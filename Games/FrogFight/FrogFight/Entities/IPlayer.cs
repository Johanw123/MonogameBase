using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GGPOSharp;

namespace FrogFight.Entities
{
  // https://github.com/genaray/Arch
  public abstract class IPlayer //: Component/*, IUpdatable*/
  {
    public virtual void Update(byte[] inputs) { }

    public int PlayerNumber = -1;
    public int NetworkHandle = -1;
    public GGPOPlayer NetworkPlayerInfo;
  }
}
