using PluginInterfaces;

namespace SmallTalkPlugin
{
    public class SmallTalkPlugin : IExecutorPlugin
    {
        public string ParameterFormat => @"usermessage:'{message}'";

        public string Name => "SmallTalkPlugin";

        public string Description => "Responds to Smalltalk like Greetings, Thank you and How are you.";

        public Task<string> ExecuteAsync(string parameter)
        {
            return Task.FromResult<string>("Respond polite and short to the origial user request.");
        }
    }
}
