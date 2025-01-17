using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using Elements.Assets;
using Elements.Core;
using System;
using WebsocketClient = Websocket.Client.WebsocketClient;

namespace InsideJoke;
public class InsideJoke : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0";
	public override string Name => "InsideJoke";
	public override string Author => "m1nt_";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/RubberDuckShobe/InsideJokeReso/";

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<string> userId = new ModConfigurationKey<string>("userName", "Username", () => "R4i");
	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<string> server = new ModConfigurationKey<string>("server", "Server IP", () => "127.0.0.1");
	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<int> port = new ModConfigurationKey<int>("port", "Port", () => 4649);
	private static ModConfiguration Config;

	private static User selectedUser;

	public static WebsocketClient ws;

	public override void OnEngineInit() {
		Config = GetConfiguration();

		Harmony harmony = new Harmony("me.m1nt.insidejoke");
		harmony.PatchAll();

		ws = new(new Uri("ws://" + Config.GetValue(server) + ":" + Config.GetValue(port)));

		ws.Start();
		ws.ReconnectionHappened.Subscribe(info => Msg($"Reconnection happened, type: {info.Type}"));
		userId.OnChanged += (newValue) => {
			Msg("Target username changed to " + newValue);
			foreach (User user in Engine.Current.WorldManager.FocusedWorld.AllUsers) {
				Debug("iterating through user " + user.UserID);
				if ((string)newValue == user.UserID) {
					Debug("User found");
					selectedUser = user;

					break;
				}
			}
		};
	}

	[HarmonyPatch(typeof(OpusStream<MonoSample>), "DecodeSamples")]
	class OpusStream_DecodeSamples_Patch {
		static void Postfix(OpusStream<MonoSample> __instance, ref float[] buffer, BitBinaryReaderX reader) {
			if (__instance.User.UserName == selectedUser.UserName) {
				Debug("Decoding " + buffer.Length + " bytes from " + __instance.User.UserName + " (" + __instance.Name + ")");

				for (int i = 0; i < buffer.Length; i++) {
					buffer[i] = MathX.Clamp(buffer[i], -1, 1);
				}

				Int16[] intData = new Int16[buffer.Length];
				Byte[] bytesData = new Byte[buffer.Length * 2];
				int rescaleFactor = 32767;
				for (int i = 0; i < buffer.Length; i++) {
					intData[i] = (short)(((float)buffer[i]) * rescaleFactor);
					Byte[] byteArr = new Byte[2];
					byteArr = BitConverter.GetBytes(intData[i]);
					byteArr.CopyTo(bytesData, i * 2);
				}

				Debug("Sending " + bytesData.Length + " bytes");

				ws.Send(bytesData);
			}
		}
	}
}
