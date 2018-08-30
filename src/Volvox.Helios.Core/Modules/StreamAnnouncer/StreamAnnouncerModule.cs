using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volvox.Helios.Core.Modules.Common;
using Volvox.Helios.Core.Utilities;
using Volvox.Helios.Domain.ModuleSettings;
using Volvox.Helios.Service.ModuleSettings;

namespace Volvox.Helios.Core.Modules.StreamAnnouncer
{
    /// <summary>
    ///     Announce the user to a specified channel when the user starts streaming.
    /// </summary>
    public class StreamAnnouncerModule : Module
    {
        private readonly IModuleSettingsService<StreamAnnouncerSettings> _settingsService;
        private IDictionary<ulong, HashSet<StreamAnnouncerMessage>> StreamingList { get; } = new Dictionary<ulong, HashSet<StreamAnnouncerMessage>>();

        /// <summary>
        ///     Announce the user to a specified channel when streaming.
        /// </summary>
        /// <param name="discordSettings">Settings used to connect to Discord.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="config">Application configuration.</param>
        /// <param name="settingsService">Settings service.</param>
        public StreamAnnouncerModule(IDiscordSettings discordSettings, ILogger<StreamAnnouncerModule> logger,
            IConfiguration config, IModuleSettingsService<StreamAnnouncerSettings> settingsService) : base(
            discordSettings, logger, config)
        {
            _settingsService = settingsService;
        }

        /// <summary>
        ///     Initialize the module on GuildMemberUpdated event.
        /// </summary>
        /// <param name="client">Client for the module to be registered to.</param>
        public override Task Init(DiscordSocketClient client)
        {
            // Subscribe to the GuildMemberUpdated event.
            client.GuildMemberUpdated += async (user, guildUser) =>
            {
                var settings = await _settingsService.GetSettingsByGuild(guildUser.Guild.Id);

                if (settings != null && settings.Enabled)
                    await CheckUser(guildUser);
            };

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Announces the user if it's appropriate to do so.
        /// </summary>
        /// <param name="user">User to be evaluated/adjusted for streaming announcement.</param>
        private async Task CheckUser(SocketGuildUser user)
        {
            // Add initial hash set for the guild.
            if (!StreamingList.TryGetValue(user.Guild.Id, out var set))
            {
                set = new HashSet<StreamAnnouncerMessage>();
                StreamingList[user.Guild.Id] = set;
            }

            // Check to make sure the user is streaming and not in the streaming list.
            if (user.Game != null && user.Game.Value.StreamType == StreamType.Twitch &&
                !StreamingList.Any(u => u.Key == user.Guild.Id && u.Value.Any(x => x.UserId == user.Id)))
            {
                var Message = new StreamAnnouncerMessage() { UserId = user.Id};

                // Add user to the streaming list.
                StreamingList[user.Guild.Id].Add(Message);

                // Announce that the user is streaming.
                await AnnounceUser(user, Message);
            }

            // User is not streaming.
            else if (user.Game == null || user.Game.Value.StreamType != StreamType.Twitch)
            {
                // Get user from streaming list.
                var UserDataFromList = StreamingList[user.Guild.Id].Where(x => x.UserId == user.Id).First();

                // Remove message from channel if neccesary.
                await AnnouncedMessagesHandler(user, UserDataFromList);

                // Remove user from list.
                StreamingList[user.Guild.Id].Remove(UserDataFromList);
            }
        }

        /// <summary>
        ///     Announces the users stream to the appropriate channel.
        /// </summary>
        /// <param name="user">User to be announced.</param>
        private async Task AnnounceUser(SocketGuildUser user, StreamAnnouncerMessage message)
        {
            // Build the embedded message.
            var embed = new EmbedBuilder()
                .WithTitle($"{user.Username} is now live!")
                .WithDescription($"{user.Game?.StreamUrl} - {user.Mention}")
                .WithColor(new Color(0x4A90E2))
                .WithThumbnailUrl(user.GetAvatarUrl())
                .AddInlineField("Title", user.Game?.Name).Build();

            // Get the settings from the database.
            var settings = await _settingsService.GetSettingsByGuild(user.Guild.Id);

            if (settings != null)
            {
                var announceChannelId = settings.AnnouncementChannelId;

                if (announceChannelId != 0)
                {
                    Logger.LogDebug($"StreamAnnouncer Module: Announcing {user.Username}");

                    // Announce the user to the channel specified in settings.
                    var MessageData = await user.Guild.GetTextChannel(announceChannelId)
                        .SendMessageAsync("", embed: embed);

                    ulong MessageId = MessageData.Id;
                    
                    // Sets MessageId in hashet, as hashset holds reference to the message param.
                    message.MessageId = MessageId;
                }
            }
        }

        /// <summary>
        ///     Remove announcement message from channel if neccesary.
        /// </summary>
        /// <param name="user">User whom stopped streaming</param>
        /// <param name="UserDataFromList">Data taken from the StreamingList hashset. 
        ///     This is where the messageId is stored
        /// </param>
        private async Task AnnouncedMessagesHandler(SocketGuildUser user, StreamAnnouncerMessage UserDataFromList)
        {
            var settings = await _settingsService.GetSettingsByGuild(user.Guild.Id);

            if (settings != null)
            {
                // Deletes messages if option is checked
                if (settings.ShouldRemoveMessagesOnStreamConclusion)
                {
                    // Announcement message Id.
                    ulong messageId = UserDataFromList.MessageId;

                    // Convert to array to work with DeleteMessagesAsync.
                    ulong[] messageIds = new ulong[1] { messageId };

                    // Delete messages
                    await user.Guild.GetTextChannel(settings.AnnouncementChannelId).DeleteMessagesAsync(messageIds);
                }
            }
        }
    }
}