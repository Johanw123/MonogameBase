using GGPOSharp;
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace FrogFight.Components
{
  public enum Facing
  {
    Left, Right
  }

  public enum State
  {
    Idle,
    Kicking,
    Punching,
    Jumping,
    Falling,
    Walking,
    Cool
  }

  [Serializable]
  [MemoryPackable]
  public partial class Player
  {
    public Facing Facing { get; set; } = Facing.Right;
    public State State { get; set; }
    public bool IsAttacking => State == State.Kicking || State == State.Punching;
    public bool CanJump => State == State.Idle || State == State.Walking;

    public bool IsLocalPlayer = false;
    public int PlayerNumber = -1;
    public int NetworkHandle = -1;

    //public Vector2 Positio

    [MemoryPackIgnore]
    public GGPOPlayer? NetworkPlayerInfo;

    public Player()
    {
      NetworkPlayerInfo = default;
      State = State.Idle;
    }
  }
}
