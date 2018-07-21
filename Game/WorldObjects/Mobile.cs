﻿using ClassicUO.Utility;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using ClassicUO.AssetsLoader;
using ClassicUO.Game.WorldObjects.Views;
using Microsoft.Xna.Framework;


namespace ClassicUO.Game.WorldObjects
{
    [Flags]
    public enum Notoriety : byte
    {
        Unknown = 0x00,
        Innocent = 0x01,
        Ally = 0x02,
        Gray = 0x03,
        Criminal = 0x04,
        Enemy = 0x05,
        Murderer = 0x06,
        Invulnerable = 0x07,
    }

    public enum RaceType : byte
    {
        HUMAN = 1,
        ELF,
        GARGOYLE
    }

    public class Mobile : Entity
    {
        private ushort _hits;
        private ushort _hitsMax;
        private ushort _mana;
        private ushort _manaMax;
        private ushort _stamina;
        private ushort _staminaMax;
        private Notoriety _notoriety;
        private bool _warMode;
        private bool _renamable;
        private bool _isSA_Poisoned;
        private RaceType _race;

        public Mobile(Serial serial) : base(serial)
        {
        }

        public event EventHandler HitsChanged;
        public event EventHandler ManaChanged;
        public event EventHandler StaminaChanged;


        protected override WorldRenderObject CreateView() => new MobileView(this);

        public new MobileView ViewObject => (MobileView)base.ViewObject;


        public RaceType Race
        {
            get => _race;
            set
            {
                if (_race != value)
                {
                    _race = value;
                    _delta |= Delta.Appearance;
                }
            }
        }

        public ushort Hits
        {
            get { return _hits; }
            set
            {
                if (_hits != value)
                {
                    _hits = value;
                    _delta |= Delta.Hits;
                }
            }
        }

        public ushort HitsMax
        {
            get { return _hitsMax; }
            set
            {
                if (_hitsMax != value)
                {
                    _hitsMax = value;
                    _delta |= Delta.Hits;
                }
            }
        }

        public ushort Mana
        {
            get { return _mana; }
            set
            {
                if (_mana != value)
                {
                    _mana = value;
                    _delta |= Delta.Mana;
                }
            }
        }

        public ushort ManaMax
        {
            get { return _manaMax; }
            set
            {
                if (_manaMax != value)
                {
                    _manaMax = value;
                    _delta |= Delta.Mana;
                }
            }
        }

        public ushort Stamina
        {
            get { return _stamina; }
            set
            {
                if (_stamina != value)
                {
                    _stamina = value;
                    _delta |= Delta.Stamina;
                }
            }
        }

        public ushort StaminaMax
        {
            get { return _staminaMax; }
            set
            {
                if (_staminaMax != value)
                {
                    _staminaMax = value;
                    _delta |= Delta.Stamina;
                }
            }
        }

        public Notoriety Notoriety
        {
            get { return _notoriety; }
            set
            {
                if (_notoriety != value)
                {
                    _notoriety = value;
                    _delta |= Delta.Attributes;
                }
            }
        }

        public bool WarMode
        {
            get { return _warMode; }
            set
            {
                if (_warMode != value)
                {
                    _warMode = value;
                    _delta |= Delta.Attributes;
                }
            }
        }

        public bool Renamable
        {
            get { return _renamable; }
            set
            {
                if (_renamable != value)
                {
                    _renamable = value;
                    _delta |= Delta.Attributes;
                }
            }
        }

        public bool Paralyzed => Flags.HasFlag(Flags.Frozen);
        public bool YellowBar => Flags.HasFlag(Flags.YellowBar);
        public bool Poisoned => FileManager.ClientVersion >= ClientVersions.CV_7000 ? _isSA_Poisoned : Flags.HasFlag(Flags.Poisoned);
        public bool Hidden => Flags.HasFlag(Flags.Hidden);
        public bool IsDead => Graphic == 402 || Graphic == 403 || Graphic == 607 || Graphic == 608 || Graphic == 970;
        public bool IsFlying => FileManager.ClientVersion >= ClientVersions.CV_7000 ? Flags.HasFlag(Flags.Flying) : false;
        public bool IsWarMode => Flags.HasFlag(Flags.WarMode);
        public bool IsHuman => 
               Utility.MathHelper.InRange(Graphic, 0x0190, 0x0193)
            || Utility.MathHelper.InRange(Graphic, 0x00B7, 0x00BA)
            || Utility.MathHelper.InRange(Graphic, 0x025D, 0x0260)
            || Utility.MathHelper.InRange(Graphic, 0x029A, 0x029B)
            || Utility.MathHelper.InRange(Graphic, 0x02B6, 0x02B7)
            || Graphic == 0x03DB || Graphic == 0x03DF || Graphic == 0x03E2;

        public void SetSAPoison(in bool value) => _isSA_Poisoned = value;


        public Item GetItemAtLayer(in Layer layer) => Equipment[(int)layer]; /*Items.FirstOrDefault(s => s.Layer == layer);*/


        protected override void OnProcessDelta(Delta d)
        {
            base.OnProcessDelta(d);
            if (d.HasFlag(Delta.Hits))
                HitsChanged.Raise(this);

            if (d.HasFlag(Delta.Mana))
                ManaChanged.Raise(this);

            if (d.HasFlag(Delta.Stamina))
                StaminaChanged.Raise(this);
        }

        public override bool Exists => World.Contains(Serial);


        public Item[] Equipment { get; } = new Item[(int)Layer.Bank + 1];


        public bool IsMounted => GetItemAtLayer(Layer.Mount) != null;
        public bool IsRunning => (Direction & Direction.Running) == Direction.Running;
        public double MoveSequence { get; set; }


        public void ClearSteps() { _steps.Clear(); Offset = Vector3.Zero; }

        public bool EnqueueStep(in Position position, in Direction direction, in bool run)
        {
            if (_steps.Count >= MAX_STEP_COUNT) return false;

            int endX = 0, endY = 0;
            sbyte endZ = 0;
            Direction endDir = Direction.NONE;

            GetEndPosition(ref endX, ref endY, ref endZ, ref endDir);

            if (endX == position.X && endY == position.Y && endZ == position.Z && endDir == direction)
                return true;

            if (!IsMoving)
                LastStepTime = World.Ticks;

            Direction moveDir = CalculateDirection(endX, endY, position.X, position.Y);

            Step step = new Step();

            if (moveDir != Direction.NONE)
            {
                if (moveDir != endDir)
                {
                    step.X = endX; step.Y = endY; step.Z = endZ; step.Direction = (byte)moveDir; step.Run = run;

                    _steps.AddToBack(step);
                }
                step.X = position.X; step.Y = position.Y; step.Z = position.Z; step.Direction = (byte)moveDir; step.Run = run;
                _steps.AddToBack(step);
            }


            if (moveDir != direction)
            {
                step.X = position.X; step.Y = position.Y; step.Z = position.Z; step.Direction = (byte)direction; step.Run = run;
                _steps.AddToBack(step);
            }

            return true;
        }

        private Direction CalculateDirection( in int curX, in int curY, in int newX, in int newY)
        {
            int deltaX = newX - curX;
            int deltaY = newY - curY;

            if (deltaX > 0)
            {
                if (deltaY > 0)
                {
                    return Direction.Down;
                }
                else if (deltaY == 0)
                {
                    return Direction.East;
                }
                else
                {
                    return Direction.Right;
                }
            }
            else if (deltaX == 0)
            {
                if (deltaY > 0)
                {
                    return Direction.South;
                }
                else if (deltaY == 0)
                {
                    return Direction.NONE;
                }
                else
                {
                    return Direction.North;
                }
            }
            else
            {
                if (deltaY > 0)
                {
                    return Direction.Left;
                }
                else if (deltaY == 0)
                {
                    return Direction.West;
                }
                else
                {
                    return Direction.Up;
                }
            }
        }

        internal void GetEndPosition(ref int x, ref int y, ref sbyte z, ref Direction dir)
        {
            if (_steps.Count <= 0)
            {
                x = Position.X;
                y = Position.Y;
                z = Position.Z;
                dir = Direction;
            }
            else
            {
                Step step = _steps.Back();
                x = step.X; y = step.Y; z = step.Z; dir = (Direction)step.Direction;
            }
        }

        public byte GetAnimationGroup(in ushort checkGraphic = 0)
        {
            Graphic graphic = checkGraphic;
            if (graphic == 0)
                graphic = GetMountAnimation();

            AssetsLoader.ANIMATION_GROUPS groupIndex = AssetsLoader.Animations.GetGroupIndex(graphic);
            byte result = AnimationGroup;

            if (result != 0xFF && Serial.IsMobile && checkGraphic > 0)
            {

            }

            bool isWalking = IsWalking;
            bool isRun = IsRunning;

            if (_steps.Count > 0)
            {
                isWalking = true;
                isRun = _steps.Front().Run;
            }

            if (groupIndex == ANIMATION_GROUPS.AG_LOW)
            {
                if (isWalking)
                {
                    if (isRun)
                        result = (byte)LOW_ANIMATION_GROUP.LAG_RUN;
                    else
                        result = (byte)LOW_ANIMATION_GROUP.LAG_WALK;
                }
                else if (AnimationGroup == 0xFF)
                {
                    result = (byte)LOW_ANIMATION_GROUP.LAG_STAND;
                    AnimIndex = 0;
                }
            }
            else if (groupIndex == AssetsLoader.ANIMATION_GROUPS.AG_HIGHT)
            {
                if (isWalking)
                {
                    result = (byte)AssetsLoader.HIGHT_ANIMATION_GROUP.HAG_WALK;
                    if (isRun)
                    {
                        if (AssetsLoader.Animations.AnimationExists(graphic, (byte)AssetsLoader.HIGHT_ANIMATION_GROUP.HAG_FLY))
                            result = (byte)AssetsLoader.HIGHT_ANIMATION_GROUP.HAG_FLY;
                    }
                }
                else if (AnimationGroup == 0xFF)
                {
                    result = (byte)AssetsLoader.HIGHT_ANIMATION_GROUP.HAG_STAND;
                    AnimIndex = 0;
                }

                if (graphic == 151)
                    result++;
            }
            else if (groupIndex == AssetsLoader.ANIMATION_GROUPS.AG_PEOPLE)
            {
                bool inWar = WarMode;

                if (isWalking)
                {
                    if (isRun)
                    {
                        if (GetItemAtLayer(Layer.Mount) != null)
                            result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_ONMOUNT_RIDE_FAST;
                        else if (GetItemAtLayer(Layer.LeftHand) != null || GetItemAtLayer(Layer.RightHand) != null)
                            result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_RUN_ARMED;
                        else
                            result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_RUN_UNARMED;

                        //if (!IsHuman && !AssetsLoader.Animations.AnimationExists(graphic, result))
                        //    goto test_walk;

                    }
                    else
                    {

                        if (GetItemAtLayer(Layer.Mount) != null)
                            result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_ONMOUNT_RIDE_SLOW;
                        else if ((GetItemAtLayer(Layer.LeftHand) != null || GetItemAtLayer(Layer.RightHand) != null) && !IsDead)
                        {
                            if (inWar)
                                result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_WALK_WARMODE;
                            else
                                result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_WALK_ARMED;
                        }
                        else if (inWar && !IsDead)
                            result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_WALK_WARMODE;
                        else
                            result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_WALK_ARMED;

                    }
                }
                else if (AnimationGroup == 0xFF)
                {
                    if (GetItemAtLayer(Layer.Mount) != null)
                        result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_ONMOUNT_STAND;
                    else if (inWar && !IsDead)
                    {
                        if (GetItemAtLayer(Layer.LeftHand) != null)
                            result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_STAND_ONEHANDED_ATTACK;
                        else if (GetItemAtLayer(Layer.RightHand) != null)
                            result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_STAND_TWOHANDED_ATTACK;
                        else
                            result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_STAND_ONEHANDED_ATTACK;
                    }
                    else
                        result = (byte)AssetsLoader.PEOPLE_ANIMATION_GROUP.PAG_STAND;

                    AnimIndex = 0;
                }

                if (Race == RaceType.GARGOYLE)
                {
                    if (IsFlying)
                    {
                        if (result == 0 || result == 1)
                            result = 62;
                        else if (result == 2 || result == 3)
                            result = 63;
                        else if (result == 4)
                            result = 64;
                        else if (result == 6)
                            result = 66;
                        else if (result == 7 || result == 8)
                            result = 65;
                        else if (result >= 9 && result <= 11)
                        {
                            result = 71;
                        }
                        else if (result >= 12 && result <= 14)
                        {
                            result = 72;
                        }
                        else if (result == 15)
                        {
                            result = 62;
                        }
                        else if (result == 20)
                        {
                            result = 77;
                        }
                        else if (result == 31)
                        {
                            result = 71;
                        }
                        else if (result == 34)
                        {
                            result = 78;
                        }
                        else if (result >= 200 && result <= 259)
                        {
                            result = 75;
                        }
                        else if (result >= 260 && result <= 270)
                        {
                            result = 75;
                        }
                    }
                }
            }

            return result;
        }


        private long _lastAnimationChangeTime;

        public void ProcessAnimation()
        {
            if (_steps.Count <= 0)
                return;

            bool turnOnly = false;

            do
            {
                Step step = _steps.Front();

                int maxDelay = (int)MovementSpeed.TimeToCompleteMovement(this, step.Run) - 15;
                int delay = (int)World.Ticks - (int)LastStepTime;
                bool removeStep = (delay >= maxDelay);

                if (step.Direction == (byte)Direction)
                {
                    float framesPerTile = maxDelay / CHARACTER_ANIMATION_DELAY;
                    float frameOffset = delay / CHARACTER_ANIMATION_DELAY;

                    float x = frameOffset;
                    float y = frameOffset;

                    GetPixelOffset(step.Direction, ref x, ref y, framesPerTile);

                    Offset = new Vector3((sbyte)x, (sbyte)y, (int)(((step.Z - Position.Z) * frameOffset) * (4.0f / framesPerTile)));

                    if (this == World.Player)
                        World.Map.Center = new Point((short)step.X, (short)step.Y);

                    turnOnly = false;
                }
                else
                {
                    turnOnly = true;
                    removeStep = true;
                }

                if (removeStep)
                {
                    if (this == World.Player)
                    {
                        if (Position.X != step.X || Position.Y != step.Y || Position.Z != step.Z)
                        {

                        }

                        if (Position.Z - step.Z >= 22)
                        {
                            // oUCH!!!!
                        }
                    }

                    Position = new Position((ushort)step.X, (ushort)step.Y, step.Z);
                    Direction = (Direction)step.Direction;

                    Offset = Vector3.Zero;
                    _steps.RemoveFromFront();

                    LastStepTime = World.Ticks;

                    ProcessDelta();
                }
            }
            while (_steps.Count > 0 && turnOnly);


            if (_lastAnimationChangeTime < World.Ticks)
            {

                byte frameIndex = AnimIndex;

                frameIndex++;

                Graphic id = GetMountAnimation();
                int animGroup = GetAnimationGroup(id);
                bool mirror = false;
                byte dir = (byte)GetAnimationDirection();

                Animations.GetAnimDirection(ref dir, ref mirror);

                int currentDelay = (int)CHARACTER_ANIMATION_DELAY;

                if (id < Animations.MAX_ANIMATIONS_DATA_INDEX_COUNT && dir < 5)
                {
                    var direction = Animations.DataIndex[id].Groups[animGroup].Direction[dir];
                    Animations.AnimID = id;
                    Animations.AnimGroup = (byte)animGroup;
                    Animations.Direction = dir;

                    if (direction.FrameCount == 0)
                        Animations.LoadDirectionGroup(ref direction);

                    if (direction.Address != 0 || direction.IsUOP)
                    {
                        int fc = direction.FrameCount;

                        if (frameIndex >= fc)
                        {
                            frameIndex = 0;

                        }

                        AnimIndex = frameIndex;
                    }

                    _lastAnimationChangeTime = World.Ticks + currentDelay;
                }
            }
        }

        public Direction GetAnimationDirection()
        {
            Direction dir = (Direction & Direction.Up);

            if (_steps.Count > 0)
            {
                dir = ((Direction)_steps.Front().Direction & Direction.Up);
            }
            return dir;
        }

        private static void GetPixelOffset(in byte dir, ref float x, ref float y, in float framesPerTile)
        {
            float step_NESW_D = 44.0f / framesPerTile;
            float step_NESW = 22.0f / framesPerTile;

            int checkX = 22;
            int checkY = 22;

            switch (dir & 7)
            {
                case 0:
                    {
                        x *= step_NESW;
                        y *= -step_NESW;
                        break;
                    }
                case 1:
                    {
                        x *= step_NESW_D;
                        checkX = 44;
                        y = 0.0f;
                        break;
                    }
                case 2:
                    {
                        x *= step_NESW;
                        y *= step_NESW;
                        break;
                    }
                case 3:
                    {
                        x = 0.0f;
                        y *= step_NESW_D;
                        checkY = 44;
                        break;
                    }
                case 4:
                    {
                        x *= -step_NESW;
                        y *= step_NESW;
                        break;
                    }
                case 5:
                    {
                        x *= -step_NESW_D;
                        checkX = 44;
                        y = 0.0f;
                        break;
                    }
                case 6:
                    {
                        x *= -step_NESW;
                        y *= -step_NESW;
                        break;
                    }
                case 7:
                    {
                        x = 0.0f;
                        y *= -step_NESW_D;
                        checkY = 44;
                        break;
                    }
                default:
                    break;
            }

            int valueX = (int)x;

            if (Math.Abs(valueX) > checkX)
            {
                if (valueX < 0)
                    x = -checkX;
                else
                    x = checkX;
            }

            int valueY = (int)y;

            if (Math.Abs(valueY) > checkY)
            {
                if (valueY < 0)
                    y = -checkY;
                else
                    y = checkY;
            }
        }

        public Vector3 Offset { get; set; }

        protected const int MAX_STEP_COUNT = 5;
        protected const int TURN_DELAY = 100;
        protected const int WALKING_DELAY = 750;
        const float CHARACTER_ANIMATION_DELAY = 80;

        protected struct Step
        {
            public int X, Y;
            public sbyte Z;

            public byte Direction;
            public bool Anim;
            public bool Run;
            public byte Seq;
        }

        protected readonly Deque<Step> _steps = new Deque<Step>();

        public long LastStepTime { get; set; }

        public bool IsWalking => LastStepTime > (World.Ticks - WALKING_DELAY);
        public byte AnimationGroup { get; set; } = 0xFF;
        public byte AnimIndex { get; set; }
        internal bool IsMoving => _steps.Count > 0;

        public Graphic GetMountAnimation()
        {
            ushort g = Graphic;

            switch (g)
            {
                case 0x0192:
                case 0x0193:
                    {
                        g -= 2;
                        break;
                    }
                default:
                    break;
            }

            return g;
        }

    }
}
