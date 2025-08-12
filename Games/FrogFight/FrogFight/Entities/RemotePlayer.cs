using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FrogFight.Entities
{
  internal class RemotePlayer : IPlayer
  {
    // public override void OnAddedToEntity()
    // {
    //   Texture2D texture = null;
    //   if (PlayerNumber == 1)
    //   {
    //     texture = Entity.Scene.Content.LoadTexture("textures/sam");
    //     Entity.SetPosition(200, 200);
    //   }
    //   else
    //   {
    //     texture = Entity.Scene.Content.LoadTexture("textures/alex");
    //     Entity.SetPosition(500, 500);
    //   }
    //   
    //   Entity.AddComponent(new SpriteRenderer(texture));
    //   Entity.Scale = Vector2.One * 0.2f; 
    // }
    //
    public override void Update(byte[] inputs)
    {
      if (inputs[0] != 0)
      {
        Console.WriteLine("Button Down: " + PlayerNumber);
        //Entity.SetScale(0.5f);
      }
      else
      {
        //Entity.SetScale(0.2f);
      }
    }
  }
}
