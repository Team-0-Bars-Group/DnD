﻿using Contracts;
using Contracts.Character;
﻿using AutoMapper;
using Contracts.Online;
using Domain.Entities.Characters;
using Domain.Entities.Parties;
using Domain.Exceptions;
using MongoDB.Driver;
using Service.Abstractions;
using DataAccess.Extensions;
using Domain.Entities;
using System.Diagnostics;
using MassTransit;
using Services.Implementation.Consumers.Characters;
using MassTransit.Initializers;
using Domain.Entities.Races;
using Domain.Entities.Classes;
using Domain.Entities.Game.Items;
using Domain.Entities.Game.Races;

namespace Services.Implementation;

public class CharacterService : ICharacterService
{
    private readonly IMongoCollection<Party> _partyCollection;
    private readonly IMongoCollection<Character> _characterCollection;
    private readonly IMongoCollection<Race> _raceCollection;
    private readonly IMongoCollection<Class> _classCollection;
    private readonly IMapper _mapper;
    private readonly IBus _eventBus;

    public CharacterService(
        IMongoCollection<Party> partyCollection,
        IMongoCollection<Character> characterCollection,
        IMongoCollection<Race> raceCollection,
        IMongoCollection<Class> classCollection,
        IMapper mapper,
        IBus eventBus
        )
    {
        _partyCollection = partyCollection;
        _characterCollection = characterCollection;
        _classCollection = classCollection;
        _raceCollection = raceCollection;
        _mapper = mapper;
        _eventBus = eventBus;
    }
    
    public async Task<Guid> CreateCharacterAsync(Guid issuer, CreateCharacterDto characterCreate)
    {
        //todo(не делать): add dto validation in CreateCharacterWithDatabaseDataAsync or before
        var character = await CreateCharacterWithDatabaseDataAsync(issuer, characterCreate);

        await _characterCollection.InsertOneAsync(character);

        return character.Id;
    }

    public async Task<GameCharacterDto> GetByIdAsync(Guid userId, Guid partyId)
    {
        var partyExists = await _partyCollection
            .FindById(partyId)
            .AnyAsync(); 
        
        if (!partyExists) 
        {
            throw new ObjectNotFoundException();
        }

        var character = await _characterCollection
            .FindByOwnerAndParty(userId, partyId)
            .SingleOrDefaultAsync()
            ?? throw new ObjectNotFoundException();
        
        return _mapper.Map<GameCharacterDto>(character);
    }

    public async Task<(bool IsDead, int InitiativeModifier)?> GetCharacterFightOrderCalculationParametersAsync(Guid characterId)
    {
        var character = await _characterCollection
            .Find(c => c.Id == characterId)
            .SingleOrDefaultAsync() 
            ?? throw new ObjectNotFoundException();

        var isDead = character.Info.IsDead;
        var initiativeModifier = character.Stats.InitiativeModifier;
        return (isDead, initiativeModifier);
    }

    public async Task<CharacterDto?> GetCharacterForUserAsync(Guid userId, Guid characterId)
    {
        var character = await _characterCollection
            .Find(c => c.Id == characterId)
            .SingleOrDefaultAsync();

        if (character == null) 
        {
            return default;
        }
        
        var notOwnerIsTryingToReadPrivateCharacter = character.Info.AccessType == AccessType.Private
                                                    && character.Info.OwnerId != userId;

        if (notOwnerIsTryingToReadPrivateCharacter)
        {
            return default;
        }

        return _mapper.Map<CharacterDto>(character!);
    }

    public async Task<IEnumerable<CharacterDto>> GetUserCharactersAsync(Guid userId)
    {
        var characters = await _characterCollection
            .Find(x => x.Info.OwnerId == userId)
            .ToListAsync();

        return characters
            .Select(_mapper.Map<CharacterDto>)
            .ToArray();
    }

    public async Task TakeDamageAsync(Guid characterId, int damage)
    {
        if (damage < 0)
        {
            throw new InvalidArgumentValueException(nameof(damage), "Урон должен быть не отрицательным")
            {
                InvalidValue = damage,
            };
        }

        var character = await _characterCollection
            .FindById(characterId)
            .SingleOrDefaultAsync() 
            ?? throw new ObjectNotFoundException();
        
        character.TakeDamage(damage);
        Debug.Assert(character.InGameStats != null);

        var selector = Builders<Character>.Filter
            .Eq(c => c.Id, characterId);
        var updateHealthAndLiveStatus = Builders<Character>.Update
            .Set(dbCharacter => dbCharacter.InGameStats!.TemporaryHitPoints, character.InGameStats.TemporaryHitPoints)
            .Set(dbCharacter => dbCharacter.InGameStats!.HitPoints, character.InGameStats.HitPoints)
            .Set(dbCharacter => dbCharacter.InGameStats!.IsDying, character.InGameStats.IsDying)
            .Set(dbCharacter => dbCharacter.InGameStats!.DeathSavesFailureCount, character.InGameStats.DeathSavesFailureCount)
            .Set(dbCharacter => dbCharacter.Info.IsDead, character.Info.IsDead);
        await _characterCollection.UpdateOneAsync(selector, updateHealthAndLiveStatus);

        await PublishCharacterUpdatedEventAsync(characterId);
    }

    public async Task UpdateCharacterInGameStatsAsync(Guid characterId, InGameStatsUpdateDto updateStats)
    {
        //todo: validate update variables before call
        // question need we check if hp is greater than max hp?

        var hp = updateStats.Hp;
        if (!updateStats.IsDead && updateStats.Hp == 0)
        {
            hp = 1;
        }

        var selector = Builders<Character>.Filter
            .Eq(c => c.Id, characterId);
        var update = Builders<Character>.Update
            .Set(c => c.InGameStats.HitPoints, hp)
            .Set(c => c.InGameStats.TemporaryHitPoints, updateStats.TempHp)
            .Set(c => c.InGameStats.InspirationBonus, updateStats.Inspiration)
            .Set(c => c.InGameStats.ActualSpeed, updateStats.Speed)
            .Set(c => c.InGameStats.HitDicesLeft, updateStats.HitDicesLeftCount)
            .Set(c => c.InGameStats.IsDying, updateStats.IsDying)
            .Set(c => c.InGameStats.DeathSavesSuccessCount, updateStats.DeathSaves?.SuccessCount ?? 0)
            .Set(c => c.InGameStats.DeathSavesFailureCount, updateStats.DeathSaves?.FailureCount ?? 0)
            .Set(c => c.Info.IsDead, updateStats.IsDead);
        await _characterCollection.UpdateOneAsync(selector, update);

        await PublishCharacterUpdatedEventAsync(characterId);
    }

    private Task PublishCharacterUpdatedEventAsync(Guid characterId)
    => _eventBus.Publish(new CharacterUpdatedEvent {Id = characterId});

    private async Task<Character> CreateCharacterWithDatabaseDataAsync(Guid issuer, CreateCharacterDto characterCreate) 
    {
        var race = await _raceCollection.Find(x => x.Id == characterCreate.Race)
            .SingleOrDefaultAsync() ?? throw new ObjectNotFoundException();

        var @class = await _classCollection.Find(x => x.Id == characterCreate.Class)
            .SingleOrDefaultAsync() ?? throw new ObjectNotFoundException();
 
        var raceName = CreateCorrectRaceName(race.SubRacesAdjustments, characterCreate.Race, characterCreate.MaybeSubrace)!;
        var personality = new CharacterPersonality(
            name: characterCreate.MaybeName ?? "",
            image: !string.IsNullOrEmpty(characterCreate.MaybeBase64Image) ? Convert.FromBase64String(characterCreate.MaybeBase64Image) : null,
            age: characterCreate.Age,
            raceName: raceName,
            startRaceTraits: GetCompleteRaceTraitsList(race, raceName, characterCreate.RaceTraitsAdjustments),
            characterCreate.Class,
            startClassFeatures: @class.ClassFeatures.Where(x => x.MinCharacterRequiredLevel == 1).ToList(),
            startXp: characterCreate.Xp,
            alignment: characterCreate.Alignment,
            background: characterCreate.MaybeBackground ?? "",
            bonds: characterCreate.MaybeBonds??[],
            flaws: characterCreate.MaybeFlaws??[],
            languages: characterCreate.MaybeLanguages??[],
            otherTraits: characterCreate.MaybeOtherTraits??[],
            size: race.Size
        );

        var management = new CharacterManagement(
            characterCreate.IsPublic ? AccessType.Public : AccessType.Private, 
            ownerId: issuer,
            startXp: characterCreate.Xp
        );
        
        var startWealth = characterCreate.StartWealth;
        var initialWallet = new CharacterCurrency(
            copper: startWealth.CopperCoins,
            silver: startWealth.SilverCoins,
            electrum: startWealth.ElectrumCoins,
            gold: startWealth.GoldCoins,
            platinum: startWealth.PlatinumCoins
        );

        var inventory = new CharacterInventory(
            setCurrencyWeightEmulationOn: characterCreate.CoinsAffectOnWeight,
            initialWallet: initialWallet,
            initialItems: characterCreate?.MaybeStartInventory
                                          ?.Where(x => x.IsValidItemDescriptor())
                                          .Where(x => x.Count > 0)
                                          .Select(x => new InventoryItem(x.InUse, x.IsItemProficiencyOn, x.Count, x.GetItem())) ?? []
                        
        );

        return new Character(
            setUpPersonality: personality,
            setUpStats: CreateCharacterStats(characterCreate!, race, @class, raceName),
            setUpInfo: management,
            startInventory: inventory
        );
    }

    private static CharacterStats CreateCharacterStats(CreateCharacterDto characterCreate, Race race, Class @class, RaceName raceName)
    {
        (var strength, var dexterity, var constitution, var intelligence, var wisdom, var charisma) 
            = GetAdjustedAbilities(characterCreate, race, raceName.SubRaceName);

        return new CharacterStats(
            strength,
            dexterity,
            constitution,
            intelligence,
            wisdom,
            charisma,
            characterCreate.Speed,
            characterCreate.SkillTraits,
            @class.SavingThrowsTraitsMastery,
            @class.HitDice
            );
    }

    private static (int Strength, int Dexterity, int Constitution, int Intelligence, int Wisdom, int Charisma) 
        GetAdjustedAbilities(CreateCharacterDto characterCreate, Race race, string? subRace)
    {
        var abilityBuffs = race.Abilities;

        AbilityBuff[] subRaceAbilities;
        if (string.IsNullOrEmpty(subRace))
        {
            subRaceAbilities = Array.Empty<AbilityBuff>();
        }
        else
        {
            subRaceAbilities = race.GetSubRaceInfo(subRace!)?.Abilities ?? Array.Empty<AbilityBuff>();
        }

        return (Strength: GetAbilityValueOr20(characterCreate.Strength + GetBuffValue(abilityBuffs, CharacterAbilityType.Strength) + GetBuffValue(subRaceAbilities, CharacterAbilityType.Strength)),
            Dexterity: GetAbilityValueOr20(characterCreate.Dexterity + GetBuffValue(abilityBuffs, CharacterAbilityType.Dexterity) + GetBuffValue(subRaceAbilities, CharacterAbilityType.Dexterity)),
            Constitution: GetAbilityValueOr20(characterCreate.Constitution + GetBuffValue(abilityBuffs, CharacterAbilityType.Constitution) + GetBuffValue(subRaceAbilities, CharacterAbilityType.Constitution)),
            Intelligence: GetAbilityValueOr20(characterCreate.Intelligence + GetBuffValue(abilityBuffs, CharacterAbilityType.Intelligence) + GetBuffValue(subRaceAbilities, CharacterAbilityType.Intelligence)),
            Wisdom: GetAbilityValueOr20(characterCreate.Wisdom + GetBuffValue(abilityBuffs, CharacterAbilityType.Wisdom) + GetBuffValue(subRaceAbilities, CharacterAbilityType.Wisdom)),
            Charisma: GetAbilityValueOr20(characterCreate.Charisma + GetBuffValue(abilityBuffs, CharacterAbilityType.Charisma) + GetBuffValue(subRaceAbilities, CharacterAbilityType.Charisma))
            );
    }

    private static int GetAbilityValueOr20(int abilityValue) => Math.Min(abilityValue, 20);

    private static int GetBuffValue(AbilityBuff[] abilityBuffs, CharacterAbilityType abilityType)
    {
        return abilityBuffs
            .Where(buff => buff.AbilityType == abilityType)
            .Sum(buff => buff.BuffValue);
    }
    private static RaceName CreateCorrectRaceName(SubRaceInfo[]? subRacesAdjustments, RaceType requestedRaceId, string? maybeRace) 
    {
        var raceHasSubraces = subRacesAdjustments != null;

        if (raceHasSubraces)
        {
            if (string.IsNullOrEmpty(maybeRace))
            {
                maybeRace = subRacesAdjustments!.FirstOrDefault()?.Name;
            }
            else
            {
                var index = Array.IndexOf(subRacesAdjustments
                    !.Select(x => x.Name.ToLower())
                    .ToArray(), maybeRace!.ToLower());

                maybeRace = index == -1 ? subRacesAdjustments!.FirstOrDefault()?.Name : subRacesAdjustments![index].Name;
            }
        }
        else 
        {
            maybeRace = null;
        }

        return new RaceName(requestedRaceId, maybeRace);
    }

    private static List<RaceTrait> GetCompleteRaceTraitsList(Race raceFullInfo, RaceName raceName, Dictionary<string, int> selectedRaceTraitsOptions)
    {
        var raceTraits = raceFullInfo
            .RaceTraits
            .Select(x => ProcessSingleRaceTrait(x, selectedRaceTraitsOptions));

        if (raceFullInfo.HasSubraces)
        {
            var subraceAdjustments = raceFullInfo
                .GetSubRaceInfo(raceName.SubRaceName!)!
                .RaceTraits
                .Select(x => ProcessSingleRaceTrait(x, selectedRaceTraitsOptions));

            raceTraits = raceTraits.Concat(subraceAdjustments);
        }

        return raceTraits.ToList();
    }

    private static RaceTrait ProcessSingleRaceTrait(RaceTraitDescriptor raceTraitDescriptor, Dictionary<string, int> selectedRaceTraitsOptions)
    {
        var maybeOptions = raceTraitDescriptor.Options;
        var hasOptions = maybeOptions != null && maybeOptions.Length > 0;
        var description = raceTraitDescriptor.Description;

        if (hasOptions)
        {
            var defentlyOptions = maybeOptions!;
            var optionIsRepresented = selectedRaceTraitsOptions.TryGetValue(raceTraitDescriptor.Name, out var selectedOptionIndex)
                && selectedOptionIndex < maybeOptions!.Length && selectedOptionIndex >= 0;

            var option = optionIsRepresented ? defentlyOptions[selectedOptionIndex] : defentlyOptions.First();

            description = $"{char.ToUpper(option[0])}{option.Substring(1)}";
        }

        return new RaceTrait(raceTraitDescriptor.Name, description);
    }
}
