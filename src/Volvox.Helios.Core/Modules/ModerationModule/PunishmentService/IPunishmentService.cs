﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using Volvox.Helios.Domain.Module.ModerationModule.Common;

namespace Volvox.Helios.Core.Modules.ModerationModule.PunishmentService
{
    public interface IPunishmentService
    {
        /// <summary>
        ///     Apply list of punishments.
        /// </summary>
        /// <param name="punishments">Punishments to apply</param>
        /// <param name="user">User whom we should apply punishments to.</param>
        /// <returns></returns>
        Task ApplyPunishments(List<Punishment> punishments, SocketGuildUser user);

        Task RemovePunishment(ActivePunishment punishment);

        Task RemovePunishmentBulk(List<ActivePunishment> punishments);
    }
}