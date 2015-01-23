namespace DataGatheringApplication.DataObjects
{
    public struct Key
    {
        public string ApiKey;
        public int Counter;
        public string Name;

        public Key(string apikey, string name, int counter)
        {
            ApiKey = apikey;
            Name = name;
            Counter = counter;
        }
    }
}