using System;
using ProtoBuf;

namespace SpaceCraft.Utils {

  [ProtoContract]
  public class Message {

    [ProtoMember(1)]
    public ulong SteamUserId {get; set;}

    [ProtoMember(2)]
    public long PlayerID {get; set;}

    [ProtoMember(3)]
    public string Text {get; set;}

    [ProtoMember(4)]
    public string Sender {get; set;}

    public Message() {
      Text = Sender = String.Empty;
      SteamUserId = 0;
      PlayerID = 0;
    }

  }
}
