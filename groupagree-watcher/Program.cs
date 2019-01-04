using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TLSharp.Core;

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
            //get available contacts
            var result = await client.GetContactsAsync();

            //find recipient in contacts
            var userToSendTo = result.Users
                .Where(x => x.GetType() == typeof(TLUser))
                .Cast<TLUser>()
                .FirstOrDefault(x => x.FirstName == "Jan");

            //send message
            await client.SendMessageAsync(new TLInputPeerUser() { UserId = userToSendTo.Id }, "OUR_MESSAGE");

            string UserNameToSendMessage = "@corgigroupagreebot";
            var unameResult = await client.SearchUserAsync(UserNameToSendMessage);

            var userByName = unameResult.Users
                .Where(x => x.GetType() == typeof(TLUser))
                .OfType<TLUser>()
                .FirstOrDefault(x => x.Username == UserNameToSendMessage.TrimStart('@'));
            for (int i = 0; i < 10; i++)
            {
                TLInputPeerUser botToCheck = new TLInputPeerUser() { UserId = userByName.Id, AccessHash = (long)userByName.AccessHash };
                await client.SendMessageAsync(botToCheck, "/start");
                await Task.Delay(TimeSpan.FromSeconds(5));
                TLAbsMessages history = await client.GetHistoryAsync(botToCheck, limit: 1);
                Console.WriteLine(history.ToString());
                TLMessagesSlice slice = (TLMessagesSlice)history;
                var message = ((TLMessage)slice.Messages.ElementAt(0));
                if (IsBitSet(message.Flags, 2) && IsBitSet(message.Flags, 1) && message.Message.StartsWith("Hey, good to see you again"))
                {
                    Console.WriteLine("testing");
                }
                Console.WriteLine(Convert.ToString(message.Flags, 2));
                var request = new TLRequestReadHistory();
                request.Peer = botToCheck;
                await client.SendRequestAsync<TLAffectedMessages>(request);
            }
        }

        public static bool IsBitSet(int value, int pos)
        { 
            return (value & (1 << pos)) != 0;
        }
    }
}
