﻿using Domain.Entities.Parties;
namespace Contracts.Parties;

public record UserPartyDto
{
    public Guid Id { get; init; }

    public Guid GameMasterId { get; init; }

    public List<Guid> InGameCharactersIds { get; init; }

    public string AccessCode { get; init; }

    public PartyCharacterDto? InGameCharacter { get; init; }

    private UserPartyDto() { }

    public static UserPartyDto FromPartyAndCharacterInfo(Party party, Guid characterId, string characterName)
    {
        return new UserPartyDto()
        {
            AccessCode = party.AccessCode,
            GameMasterId = party.GameMasterId,
            Id = party.Id,
            InGameCharacter = new PartyCharacterDto
            {
                Id = characterId,
                CharacterName = characterName
            },
        };
    }

}
