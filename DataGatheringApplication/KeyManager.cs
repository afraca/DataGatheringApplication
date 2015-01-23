using System;
using System.Linq;
using DataGatheringApplication.DataObjects;

namespace DataGatheringApplication
{
    internal class KeyManager
    {
        private readonly Key[] _keylist;
        private readonly object _syncLock = new object();

        public KeyManager()
        {
            // Just hardcode these in.
            // Update the use count by checking https://www.openhub.net/accounts/afraca/api_keys 

            _keylist = new[]
            {
                new Key("your-api-key-here", "Description", 0)
            };
        }

        public string GetApiKey()
        {
            lock (_syncLock)
            {
                for (var i = 0; i < _keylist.Count(); i++)
                {
                    if (_keylist[i].Counter < 997)
                    {
                        _keylist[i].Counter++;
                        return _keylist[i].ApiKey;
                    }
                }
                throw new Exception("Out of ApiKeys");
            }
        }
    }
}