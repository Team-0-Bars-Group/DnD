﻿namespace Domain.Entities.Races;

public class RaceTrait
{
    public RaceTrait(string name, string description)
    {
        Name = name;
        Description = description;
    }

    protected RaceTrait() { }

    public int Id { get; protected set; }

    public string Name { get; protected set; } 

    public string Description { get; protected set; }
}
