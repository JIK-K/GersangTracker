using System;
using System.Collections.Generic;

namespace GersangTracker.Models
{
    public enum GameServer
    {
        Korea_Live,
        Korea_Test,
        Korea_RnD
    }

    public record GameServerOption(GameServer Server, string DisplayName);
}