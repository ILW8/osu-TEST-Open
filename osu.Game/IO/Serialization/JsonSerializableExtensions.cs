// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Framework.IO.Serialization;

namespace osu.Game.IO.Serialization
{
    public static class JsonSerializableExtensions
    {
        public static string Serialize(this object obj) => JsonConvert.SerializeObject(obj, JsonSerializerSettings);

        public static T Deserialize<T>(this string objString) => JsonConvert.DeserializeObject<T>(objString, JsonSerializerSettings);

        public static void DeserializeInto<T>(this string objString, T target) => JsonConvert.PopulateObject(objString, target, JsonSerializerSettings);

        private static JsonSerializerSettings createGlobalSettings() => new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            Converters = new List<JsonConverter> { new Vector2Converter() },
            ContractResolver = new SnakeCaseKeyContractResolver()
        };

        public static JsonSerializerSettings JsonSerializerSettings => createGlobalSettings();
    }
}
