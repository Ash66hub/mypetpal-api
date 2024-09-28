using System.Text.Json.Serialization;

namespace mypetpal.Data.Common.Enums
{
	public class PetEnums
	{
        public enum PetStatus
        {
            Neutral,
            Happy,
            Sad,
            Dead
        }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum PetTypes
        {
            Dogo,
            Cato
        }
    }
}

