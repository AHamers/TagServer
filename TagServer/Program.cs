using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TagServer
{
	static class Program
	{
		private static TcpListener socket;
		private static List<ClientThread> clientThreads;
		private static int currentTagPlayerID;

		static void Main(string[] args)
		{
			Console.WriteLine("==========SERVER ON==========");
			clientThreads = new List<ClientThread>();
			currentTagPlayerID = 0;

			socket = new TcpListener(IPAddress.Parse("192.168.0.42"), 8052);
			socket.Start();

			while (true)
			{
				TcpClient newClient;
				newClient = socket.AcceptTcpClient();
				ClientThread newClientThread = new ClientThread(newClient, clientThreads.Count);
				clientThreads.Add(newClientThread);

				//indicating new player game infos (other's names, tag)
				for (int i = 0; i < clientThreads.Count; i++)
                {
					if(clientThreads[i].clientID != newClientThread.clientID)
						newClientThread.sendMessage("NAME|" + clientThreads[i].clientID + "|" + clientThreads[i].clientName); //indicating player other's names and IDs
					else
						newClientThread.sendMessage("NAME|" + clientThreads[i].clientID + "|YOURSELF"); //indicate player it's own ID
				}
				newClientThread.sendMessage("TAG|" + currentTagPlayerID);
			}
		}

		/* NAME|<playerName> Received Name information from client. Must rename it in program.
		 * POS|<Vec3Pos> Received position information from client. Must redirect information to all other clients
		 * ROT|<QuaternionRot>
		 * COLL|<OtherID>|<Vec3CollisionPoint>
		 * DASH|<Vec3FXPosition>|<vec3FXDirection
		 * JUMPER|<vec3position>|<vec3forward>
		 * DISC Received disconnect information from client. Must delete it from list
		 */
		static void handleMessage(string[] args, int emitterIndexInArray)
        {
			switch (args[0])
			{
				case "NAME":
                    {
						if (args.Length < 2)
							break;
						clientThreads[emitterIndexInArray].clientName = args[1];
						for (int i = 0; i < clientThreads.Count; i++)
						{
							if (i != emitterIndexInArray)
								clientThreads[i].sendMessage("NAME|" + clientThreads[emitterIndexInArray].clientID + "|" + args[1]);
						}
						break;
                    }
				case "POS":
					{
						if (args.Length < 2)
							break;
						for (int i = 0; i < clientThreads.Count; i++)
                        {
							if (clientThreads[i].clientID == clientThreads[emitterIndexInArray].clientID)
								continue;

							clientThreads[i].sendMessage("POS|" + clientThreads[emitterIndexInArray].clientID + "|" + args[1]);
						}
						break;
					}
				case "ROT":
                    {
						if (args.Length < 2)
							break;
						for (int i = 0; i < clientThreads.Count; i++)
						{
							if (clientThreads[i].clientID == clientThreads[emitterIndexInArray].clientID)
								continue;

							clientThreads[i].sendMessage("ROT|" + clientThreads[emitterIndexInArray].clientID + "|" + args[1]);
						}
						break;
                    }
				case "COLL":
                    {
						if (args.Length < 3)
							break;
						int otherID = int.Parse(args[1]);
						int otherIndexInArray = -1;
						for(int i = 0; i < clientThreads.Count; i++)
                        {
							if (clientThreads[i].clientID == otherID)
								otherIndexInArray = i;
                        }
						if (otherIndexInArray == -1)
							break;

						if (!clientThreads[otherIndexInArray].isCollisionCooldownRunning && !clientThreads[emitterIndexInArray].isCollisionCooldownRunning)
						{
							clientThreads[otherIndexInArray].sendMessage("COLL|" + args[2]);
							Thread collisionCoolDown1 = new Thread(clientThreads[otherIndexInArray].startCollisionCooldown);
							collisionCoolDown1.Start();
							clientThreads[emitterIndexInArray].sendMessage("COLL|" + args[2]);
							Thread collisionCoolDown2 = new Thread(clientThreads[emitterIndexInArray].startCollisionCooldown);
							collisionCoolDown2.Start();

							if (currentTagPlayerID == clientThreads[emitterIndexInArray].clientID)
							{
								currentTagPlayerID = otherID;
								broadcastTagMessage();
							}
							else if (currentTagPlayerID == otherID)
							{
								currentTagPlayerID = clientThreads[emitterIndexInArray].clientID;
								broadcastTagMessage();
							}
						}
						break;
                    }
				case "DASH":
					{
						if (args.Length < 3)
							break;
						for (int i = 0; i < clientThreads.Count; i++)
						{
							if (clientThreads[i].clientID == clientThreads[emitterIndexInArray].clientID)
								continue;

							clientThreads[i].sendMessage("DASH|" + args[1] + '|' + args[2]);
						}
						break;
					}
				case "JUMPER":
					{
						if (args.Length < 3)
							break;
						for (int i = 0; i < clientThreads.Count; i++)
						{
							if (clientThreads[i].clientID == clientThreads[emitterIndexInArray].clientID)
								continue;

							clientThreads[i].sendMessage("JUMPER|" + args[1]+ "|" + args[2]);
						}
						break;
					}
				case "DISC":
					{
						int disconnectingClientID = clientThreads[emitterIndexInArray].clientID;

						clientThreads[emitterIndexInArray].connectedClient.Close();
						clientThreads[emitterIndexInArray].endAsked = true;
						clientThreads.RemoveAt(emitterIndexInArray);

						for (int i = 0; i < clientThreads.Count; i++)
						{
							clientThreads[i].indexInArray = i;
							clientThreads[i].sendMessage("DISC|" + disconnectingClientID);
						}

						if (disconnectingClientID == currentTagPlayerID)
						{
							Random rand = new Random();
							if (clientThreads.Count != 0)
							{
								int newTagIndex = rand.Next() % clientThreads.Count;
								currentTagPlayerID = clientThreads[newTagIndex].clientID;
							}
							else
                            {
								currentTagPlayerID = ClientThread.clientIDCounter;
                            }

							for(int i = 0; i < clientThreads.Count; i++)
                            {
								clientThreads[i].sendMessage("TAG|"+currentTagPlayerID);
                            }
						}

						break;
                    }
				default:
					{
						break;
					}
			}

        }

		private static void broadcastTagMessage()
        {
			for(int i = 0; i < clientThreads.Count; i++)
            {
				clientThreads[i].sendMessage("TAG|" + currentTagPlayerID);
				for(int j = 0; j < clientThreads.Count; j++)
                {
					if(clientThreads[j].clientID == currentTagPlayerID)
                    {
						Console.WriteLine(clientThreads[j].clientName + " is Tag !");
                    }
                }
            }
        }

		class ClientThread
		{
			public Thread thread;
			public TcpClient connectedClient;
			public string clientName = null;
			public bool endAsked = false;
			public int clientID;
			public int indexInArray;
			public static int clientIDCounter = 0;

			public bool isCollisionCooldownRunning;

			public ClientThread(TcpClient client, int indexInArray)
			{
				this.connectedClient = client;
				this.indexInArray = indexInArray;
				this.clientID = ClientThread.clientIDCounter;
				ClientThread.clientIDCounter++;
				thread = new Thread(new ThreadStart(listenForMessages));
				//thread.IsBackground = true;
				isCollisionCooldownRunning = false;
				thread.Start();
				Console.WriteLine("[SERVER]Connexion established with client " + clientID);
			}

			public void startCollisionCooldown()
            {
				isCollisionCooldownRunning = true;
				Console.WriteLine("Cooldown for " + this.clientName);
				Thread.Sleep(1000);
				isCollisionCooldownRunning = false;
				Console.WriteLine("End of cooldown for " + this.clientName);
			}

			private void listenForMessages()
			{
				try
				{
					while (true)
					{
						if (endAsked)
							return;
						Byte[] bytes = new Byte[1024];
						using (NetworkStream stream = connectedClient.GetStream())
						{
							int length;
							// Read incoming stream into byte arrary. 						
							while (!endAsked && (length = stream.Read(bytes, 0, bytes.Length)) != 0)
							{
								Byte[] incomingData = new byte[length];
								Array.Copy(bytes, 0, incomingData, 0, length);
								// Convert byte array to string message. 							
								string clientMessage = Encoding.ASCII.GetString(incomingData);
								string[] separatedMessages = clientMessage.Split('@');
								for (int i = 0; i < separatedMessages.Length; i++)
								{
									//Console.WriteLine(clientID + " : " + separatedMessages[i]);
									handleMessage(separatedMessages[i]);
								}
								stream.Flush();
							}
						}
					}
				}
				catch (SocketException socketException)
				{
					Console.WriteLine("[ERROR]Socket exception: " + socketException);
				}
			}

			public void handleMessage(string message)
			{
				string[] splitMessage = message.Split('|');
				Program.handleMessage(splitMessage, this.indexInArray);
			}

			public void sendMessage(string message)
			{
				message = '@' + message;

				if (connectedClient == null)
				{
					return;
				}

				try
				{
					NetworkStream stream = connectedClient.GetStream();
					if (stream.CanWrite)
					{
						stream.Flush();
						byte[] serverMessageAsByteArray = Encoding.ASCII.GetBytes(message);
						stream.Write(serverMessageAsByteArray, 0, serverMessageAsByteArray.Length);
					}
				}
				catch (SocketException socketException)
				{
					Console.WriteLine("[ERROR] - Socket exception: " + socketException);
				}
			}
		}
	}
}
