using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GGPOSharp;
using Microsoft.Xna.Framework;

namespace FrogFight.Entities
{
  //public class GameEntity
  //{
  //  public Vector2 Position { get; set; }
  //  public Vector2 Velocity { get; set; }
  //}

  // https://github.com/genaray/Arch
  public abstract class IPlayer /*: GameEntity*///: Component/*, IUpdatable*/
  {
    public virtual void Update(byte[] inputs) { }

    public int PlayerNumber = -1;
    public int NetworkHandle = -1;
    public GGPOPlayer NetworkPlayerInfo;
  }
}
