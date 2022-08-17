﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_Support_Bot.Interaction.AutoVoiceChannel.Services
{
    public class AutoVoiceChannelService : IInteractionService
    {
        private readonly DiscordSocketClient _client;

        public AutoVoiceChannelService(DiscordSocketClient client)
        {
            _client = client;
            _client.UserVoiceStateUpdated += _client_UserVoiceStateUpdated;
            _ = new Timer((obj) => 
            { 
                _ = Task.Run(async () =>
                {
                    await RemoveEmptyVoiceChannel(null);
                    
                }); 
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
        }

        private async Task RemoveEmptyVoiceChannel(object obj)
        {

            using var db = new SupportContext();
            foreach (var item in db.GuildConfig)
            {
                try
                {
                    var guild = _client.GetGuild(item.GuildId);
                    if (guild == null)
                        continue;

                    foreach (var voiceChannel in guild.VoiceChannels)
                    {
                        if (voiceChannel.Name.EndsWith("'s Room") && !voiceChannel.ConnectedUsers.Any())
                            await voiceChannel.DeleteAsync();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"RemoveEmptyVoiceChannel-{item.GuildId} ({item.AutoVoiceChannel}): {ex}");
                }
            }
        }

        private Task _client_UserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            _ = Task.Run(async () =>
            {
                if (user is not IGuildUser usr || usr.IsBot)
                    return;

                var beforeVch = before.VoiceChannel;
                var afterVch = after.VoiceChannel;

                if (beforeVch == afterVch)
                    return;

                using var db = new SupportContext();
                var guildConfig = db.GuildConfig.FirstOrDefault((x) => x.GuildId == usr.GuildId);
                if (guildConfig == null)
                    return;

                if (beforeVch?.Guild == afterVch?.Guild) // User Move
                {
                    bool isCreate = false;
                    if (afterVch.Id == guildConfig.AutoVoiceChannel)
                        isCreate = await CreateVoiceChannelAndMoveUser(usr, afterVch);
                    if (isCreate && beforeVch.Name.EndsWith("'s Room") && !beforeVch.ConnectedUsers.Any())
                        await beforeVch.DeleteAsync();
                }
                else if (beforeVch is null) // User Join
                {
                    if (afterVch.Id == guildConfig.AutoVoiceChannel)
                        await CreateVoiceChannelAndMoveUser(usr, afterVch);
                }
                else if (afterVch is null) // User Leave
                {
                    if (beforeVch.Name.EndsWith("'s Room") && !beforeVch.ConnectedUsers.Any())
                        await beforeVch.DeleteAsync();
                }
            });
            return Task.CompletedTask;
        }

        private async Task<bool> CreateVoiceChannelAndMoveUser(IGuildUser user, SocketVoiceChannel voiceChannel)
        {
            bool isCreate = false;
            try
            {
                string roomName = $"{user.Username}'s Room";
                IVoiceChannel newChannel = voiceChannel.Guild.VoiceChannels.FirstOrDefault((x) => x.Name == roomName);

                if (newChannel == null)
                {
                    newChannel = await voiceChannel.Guild.CreateVoiceChannelAsync(roomName, (act) =>
                    {
                        act.Bitrate = voiceChannel.Bitrate;
                        act.CategoryId = voiceChannel.CategoryId;
                        act.UserLimit = voiceChannel.UserLimit;
                    });
                    Log.Info($"建立語音頻道: {newChannel.Name}");
                    isCreate = true;
                }

                await voiceChannel.Guild.MoveAsync(user, newChannel);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

            return isCreate;
        }
    }
}
