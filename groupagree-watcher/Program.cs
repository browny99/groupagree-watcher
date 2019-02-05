using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TLSharp.Core;
using Renci.SshNet;

namespace groupagree_watcher
{
    class Program
    {
        static void Main(string[] args)
        {
            runAsync(args).GetAwaiter().GetResult();
        }

        static async Task runAsync(string[] args)
        {
            var config = System.IO.File.ReadAllLines("client.config");
            var connectionInfo = new ConnectionInfo(config[2],
                                        config[3],
                                        new PasswordAuthenticationMethod(config[3], config[4]));
            var sshClient = new SshClient(connectionInfo);
            var sshCommand = config[5];

            var client = new TelegramClient(int.Parse(config[0]), config[1]);
            await client.ConnectAsync();

            if (!client.IsUserAuthorized())
            {
                Console.WriteLine("Please enter your phone number");
                var number = Console.ReadLine();
                var hash = await client.SendCodeRequestAsync(number);
                Console.WriteLine("Enter the code you recieved from Telegram");
                var code = Console.ReadLine();
                TLUser user = null;
                try
                {
                    user = await client.MakeAuthAsync(number, hash, code);
                }
                catch (CloudPasswordNeededException)
                {
                    var password = await client.GetPasswordSetting();
                    Console.WriteLine("Enter your 2FA Password");
                    var password_str = Console.ReadLine();
                    user = await client.MakeAuthWithPasswordAsync(password, password_str);
                }
            }

            var result = await client.GetContactsAsync();
            
            var userToSendTo = result.Users
                .Where(x => x.GetType() == typeof(TLUser))
                .Cast<TLUser>()
                .FirstOrDefault(x => x.Username == "browny99");

            var logPeer = new TLInputPeerUser() { UserId = userToSendTo.Id };

            await client.SendMessageAsync(logPeer, "Started monitoring");

            string UserNameToSendMessage = "@corgigroupagreebot";
            var unameResult = await client.SearchUserAsync(UserNameToSendMessage);

            var userByName = unameResult.Users
                .Where(x => x.GetType() == typeof(TLUser))
                .OfType<TLUser>()
                .FirstOrDefault(x => x.Username == UserNameToSendMessage.TrimStart('@'));

            int retryCounter = 0;
            while (!System.IO.File.Exists("cancel.wjdummy"))
            {
                try
                {
                    TLInputPeerUser botToCheck = new TLInputPeerUser() { UserId = userByName.Id, AccessHash = (long)userByName.AccessHash };
                    await client.SendMessageAsync(botToCheck, "/start");

                    await Task.Delay(TimeSpan.FromSeconds(30));

                    TLAbsMessages history = await client.GetHistoryAsync(botToCheck, limit: 1);
                    TLMessagesSlice slice = (TLMessagesSlice)history;
                    var message = ((TLMessage)slice.Messages.ElementAt(0));

                    if (message.Out == false && message.Message.StartsWith("Hey, good to see you again"))
                    {
                        var request = new TLRequestReadHistory();
                        request.Peer = botToCheck;
                        await client.SendRequestAsync<TLAffectedMessages>(request);
                        retryCounter = 0;
                    }
                    else
                    {
                        retryCounter++;
                        await client.SendMessageAsync(logPeer, "30 sec unresponsive");
                    }
                    if (retryCounter > 5)
                    {
                        sshClient.Connect();
                        var res = sshClient.CreateCommand(sshCommand).Execute();
                        sshClient.Disconnect();
                        await client.SendMessageAsync(logPeer, "Restarted server\n\n" + res);
                        await Task.Delay(TimeSpan.FromSeconds(90));
                        retryCounter = 0;
                    }
                } catch (Exception e)
                {
                    try
                    {
                        await client.SendMessageAsync(logPeer, "Error: \n" + e.ToString());
                    } catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR\n\n{e}\n{ex}\n\nENDERROR");
                    }
                }
            }
        }

        public static bool IsBitSet(int value, int pos)
        { 
            return (value & (1 << pos)) != 0;
        }
    }
}
