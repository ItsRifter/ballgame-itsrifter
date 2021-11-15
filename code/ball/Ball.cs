﻿
using Sandbox;
using Sandbox.UI.Construct;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Ballers
{
	public partial class Ball : ModelEntity
	{
		public static float Acceleration = 750;
		public static float AirControl = 1f;
		public static float MaxSpeed = 1100f;

		public static float Friction = 0.25f;
		public static float Drag = 0.1f;
		public static float WallBounce = 0.25f;
		public static float FloorBounce = 0.25f;

		public static Ball Create( BallPlayer player )
		{
			var spawnpoint = Entity.All.OfType<SpawnPoint>().OrderBy( x => Guid.NewGuid() ).FirstOrDefault();

			Rotation rotation = Rotation.Identity;
			Vector3 position = Vector3.Up * 40f;
			if ( spawnpoint != null )
			{
				position += spawnpoint.Position;
			}

			Ball newBall = new Ball() { Owner = player, Position = position };
			player.Ball = newBall;

			return newBall;
		}

		private bool hasColor = false;

		public override void Spawn()
		{
			base.Spawn();

			Predictable = true;

			SetModel( "models/ball.vmdl" );

			EnableAllCollisions = false;
			EnableTraceAndQueries = false;
			Transmit = TransmitType.Always;
		}

		public override void ClientSpawn()
		{
			base.ClientSpawn();

			Predictable = true;
		}

		[Event.Frame]
		public void OnFrame()
		{
			if ( hasColor )
				return;

			if ( !SceneObject.IsValid() )
				return;

			int id = (int)(Owner.Client.PlayerId & 255);
			Random seedColor = new Random( id );
			float hue = (float)seedColor.NextDouble() * 360f;

			Color ballColor = new ColorHsv( hue, 0.8f, 1f );
			Color ballColor2 = new ColorHsv( (hue + 30f) % 360, 0.8f, 1f );

			SceneObject.SetValue( "tint", ballColor );
			SceneObject.SetValue( "tint2", ballColor2 );

			hasColor = true;
		}

		public static readonly SoundEvent WilhelmScream = new( "sounds/ball/wilhelm.vsnd" )
		{
			DistanceMax = 1536f,
		};

		public override string ToString() => $"Ball {NetworkIdent} ({Owner.Name})";
	}
}
