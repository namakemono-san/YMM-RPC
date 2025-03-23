using DiscordRPC;
using DiscordRPC.Logging;
using YukkuriMovieMaker.Plugin;

namespace YmmRPC
{
    [PluginDetails(AuthorName = "namakemono-san", ContentId = "")]
    public class YmmRpcPlugin : IPlugin
    {
        public string Name => "YMM-RPC";
        
        static YmmRpcPlugin()
        {
            var client = new DiscordRpcClient("1353376132732420136");			
	
            client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };

            client.OnReady += (sender, e) =>
            {
                Console.WriteLine("Received Ready from user {0}", e.User.Username);
            };
		
            client.OnPresenceUpdate += (sender, e) =>
            {
                Console.WriteLine("Received Update! {0}", e.Presence);
            };
	
            client.Initialize();
            
            // TODO: 作業内容を表示したい。
            client.SetPresence(new RichPresence()
            {
                Assets = new Assets()
                {
                    LargeImageKey = "icon",
                    LargeImageText = "YMM-RPC v0.1.0"
                }
            });	
        }
    }
}