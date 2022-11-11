﻿using Bang;
using Bang.Components;
using System.Collections.Immutable;
using Murder.Attributes;
using Murder.Assets.Graphics;
using Murder.Core.Geometry;
using Murder.Core.Graphics;
using Murder.Utilities;

namespace Murder.Components
{
    [Requires(typeof(ITransformComponent))]
    public readonly struct AsepriteComponent : IComponent
    {
        public readonly TargetSpriteBatches TargetSpriteBatch = TargetSpriteBatches.Gameplay;

        [GameAssetId(typeof(AsepriteAsset))]
        public readonly Guid AnimationGuid = Guid.Empty;

        [Tooltip("(0,0) is top left and (1,1) is bottom right"), Slider()]
        public readonly Vector2 Offset = Vector2.Zero;

        public readonly bool RotateWithFacing = false;

        [HideInEditor]
        public readonly string AnimationId = string.Empty;
        public readonly ImmutableArray<string> NextAnimations = ImmutableArray<string>.Empty;

        internal bool HasAnimation(string animationName)
        {
            return Game.Data.GetAsset<AsepriteAsset>(AnimationGuid).Animations.ContainsKey(animationName);
        }

        public readonly float AnimationStartedTime = 0;

        public readonly int YSortOffset = 0;

        public AsepriteComponent() { }

        public AsepriteComponent(Guid guid, Vector2 offset, string id, int ySortOffset, bool backAnim, TargetSpriteBatches targetSpriteBatch)
            : this(guid, offset, ImmutableArray.Create(id), ySortOffset, backAnim, targetSpriteBatch) { }

        public AsepriteComponent(Guid guid, Vector2 offset, ImmutableArray<string> id, int ySortOffset, bool rotate, float time, TargetSpriteBatches targetSpriteBatch)
        {
            AnimationGuid = guid;
            Offset = offset;
            AnimationId = id[0];

            NextAnimations = id.Take(new Range(1, id.Length)).ToImmutableArray();
            AnimationStartedTime = time;
            YSortOffset = ySortOffset;
            RotateWithFacing = rotate;
            TargetSpriteBatch = targetSpriteBatch;
        }
        public AsepriteComponent(Guid guid, TargetSpriteBatches targetSpriteBatch)
        {
            AnimationGuid = guid;
            Offset = Vector2.Zero;
            AnimationId = Game.Data.GetAsset<AsepriteAsset>(guid).Animations.Keys.FirstOrDefault() ?? string.Empty;

            NextAnimations = ImmutableArray<string>.Empty;
            AnimationStartedTime = Time.Elapsed;
            YSortOffset = 0;
            RotateWithFacing = false;
            TargetSpriteBatch = targetSpriteBatch;
        }

        public AsepriteComponent(Guid guid, Vector2 offset, ImmutableArray<string> id, int ySortOffset, bool backAnim, TargetSpriteBatches targetSpriteBatch) :
            this(guid, offset, id, ySortOffset, backAnim, Time.Elapsed, targetSpriteBatch)
        { }

        public AsepriteComponent Play(string id) => new AsepriteComponent(AnimationGuid, Offset, id, YSortOffset, RotateWithFacing, TargetSpriteBatch);
        public AsepriteComponent PlayOnce(string id)
        {
            if (id != AnimationId)
                return new AsepriteComponent(AnimationGuid, Offset, id, YSortOffset, RotateWithFacing, TargetSpriteBatch);
            else
                return this;
        }
        public AsepriteComponent PlayAfter(string id)
        {
            if (id != AnimationId && !NextAnimations.Contains(id))
            {
                var sequence = ImmutableArray.CreateBuilder<string>();
                sequence.Add(AnimationId);
                sequence.AddRange(NextAnimations);
                sequence.Add(id);
                return new AsepriteComponent(
                    AnimationGuid,
                    Offset,
                    sequence.ToImmutable(),
                    YSortOffset,
                    RotateWithFacing,
                    AnimationStartedTime,
                    TargetSpriteBatch);
            }
            else
                return this;
        }

        internal AsepriteComponent StartNow() => new AsepriteComponent(AnimationGuid, Offset, NextAnimations.Insert(0,AnimationId), YSortOffset, RotateWithFacing, TargetSpriteBatch);
        public AsepriteComponent Play(params string[] id) => new AsepriteComponent(AnimationGuid, Offset, id.ToImmutableArray(), YSortOffset, RotateWithFacing, TargetSpriteBatch);
        public AsepriteComponent Play(ImmutableArray<string> id) => new AsepriteComponent(
            AnimationGuid,
            Offset,
            HasAnimation(id[0]) ? id : ImmutableArray.Create(AnimationId),
            YSortOffset,
            RotateWithFacing,
            TargetSpriteBatch);

        public AsepriteComponent WithSort(int sort) => new AsepriteComponent(
            AnimationGuid,
            Offset,
            NextAnimations.Insert(0, AnimationId),
            sort,
            RotateWithFacing,
            AnimationStartedTime,
            TargetSpriteBatch);
    }
}
