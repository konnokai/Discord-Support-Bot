﻿namespace DiscordSupportBot.SQLite.Activity
{
    class UserTable
    {
        public ulong UserID { get; set; }
        public string UserName { get; set; }
        public int ActivityNum { get; set; }
    }
}