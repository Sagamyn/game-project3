public enum Faction
{
    Player,
    Enemy
}

public enum UnitType
{
    LineInfantry,
    Skirmisher,
    Cavalry,
    Artillery
}

public enum FormationType
{
    Line,
    Column,
    Square,
    Dispersed
}

public enum OrderType
{
    None,
    Move,
    Fire,
    Charge,
    Hold,
    ChangeFormation
}

public enum MoraleState
{
    Steady,     // 75-100
    Shaken,     // 50-74
    Wavering,   // 25-49
    Breaking    // 0-24
}

public enum SoldierState
{
    Alive,
    Wounded,
    Dead
}

public enum TurnPhase
{
    Planning,
    Execution,
    Results
}

public enum TerrainType
{
    Plain,
    Hill,
    Forest,
    River
}

