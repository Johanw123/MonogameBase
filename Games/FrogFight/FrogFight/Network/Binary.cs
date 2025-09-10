using FrogFight.Scenes;
using LiteNetLib.Utils;
using MemoryPack;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FrogFight.Network
{
  public static class Binary
  {
    static NetSerializer serializer = new();

    /// <summary>
    /// Convert an object to a Byte Array.
    /// </summary>
    public static byte[] ObjectToByteArray(object objData)
    {
      if (objData == null)
        return default;

      //var apa = serializer.Serialize<GameState>(objData as GameState);
      //var obj = serializer.Deserialize<GameState>(new NetDataReader(apa));

      //byte[] bytes = MessagePackSerializer.Serialize(objData);
      //GameState mc2 = MessagePackSerializer.Deserialize<GameState>(bytes);
      var gs = objData as GameState;
      var bin = MemoryPackSerializer.Serialize(gs);
      //var val = MemoryPackSerializer.Deserialize<GameState>(bin);

      return bin;
    }

    /// <summary>
    /// Convert a byte array to an Object of T.
    /// </summary>
    public static T ByteArrayToObject<T>(byte[] byteArray) where T : class, new()
    {
      if (byteArray == null || !byteArray.Any())
        return default;

      var val = MemoryPackSerializer.Deserialize<T>(byteArray);

      //var obj = serializer.Deserialize<T>(new NetDataReader(byteArray));
      return val;
    }

    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
      return new JsonSerializerOptions()
      {
        PropertyNamingPolicy = null,
        WriteIndented = true,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        //TypeInfoResolver = /*SourceGenerationContext.Default*/ 
      };
    }
  }
}
