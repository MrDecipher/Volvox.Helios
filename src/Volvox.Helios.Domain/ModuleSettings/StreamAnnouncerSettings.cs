﻿namespace Volvox.Helios.Domain.ModuleSettings
{
    public class StreamAnnouncerSettings : ModuleSettings
    {
        public ulong AnnouncementChannelId { get; set; }

        public bool ShouldRemoveMessagesOnStreamConclusion { get; set; }
    }
}