﻿
using Sandbox;
using Sandbox.UI.Construct;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ballers
{
	public partial class Ball
	{
		public bool QueueDeletion => queueDeletion;
		private bool queueDeletion = false;

		public Client Owner { get; private set; }
		public int NetworkIdent => Owner.NetworkIdent;

		public Vector3 Position { get; set; }
		public Vector3 Velocity { get; set; }
		public Vector3 MoveDirection { get; set; }
		
		public bool IsClient => Host.IsClient;
		public bool IsServer => Host.IsServer;

		public void Delete()
		{
			queueDeletion = true;

			if ( IsServer )
				ClientDelete( NetworkIdent );
			else
			{
				Sound.FromWorld( WilhelmScream.Name, Model.Position );
				DeleteModels();
				RollingSound.Stop();
			}	
		}

		public static readonly SoundEvent WilhelmScream = new( "sounds/ball/wilhelm.vsnd" )
		{
			DistanceMax = 1536f,
		};

		public void Frame()
		{
			UpdateTerry();
		}

		public override int GetHashCode() => NetworkIdent;
		public override string ToString() => $"Ball {NetworkIdent} ({Owner.Name})";
	}
}
