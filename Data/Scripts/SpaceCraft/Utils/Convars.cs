using System;

namespace SpaceCraft.Utils {

  public sealed class Convars {

    private static volatile Convars instance = new Convars();

    public static Convars Static
    {
      get { lock(instance){return instance;} }
    }

    public volatile float Difficulty = 1f;
    public volatile int Grids = 20;
    public volatile bool Debug = false;
    public volatile int Engineers = 1;

    private Convars(){
      bool spawned;
      MyAPIGateway.Utilities.GetVariable<bool>("SC-Spawned", out spawned);
      if( spawned ) {
  			MyAPIGateway.Utilities.GetVariable<int>("SC-Grids", out Grids);
  			MyAPIGateway.Utilities.GetVariable<float>("SC-Difficulty", out Difficulty);
  			MyAPIGateway.Utilities.GetVariable<int>("SC-Engineers", out Engineers);
  			MyAPIGateway.Utilities.GetVariable<bool>("SC-Debug", out Debug);
      }
    }

    public string Set( string convar, string value ) {
      switch( convar.ToLower() ) {
        case "engineers":
          Int32.TryParse(value, out Engineers);
          MyAPIGateway.Utilities.SetVariable<int>("SC-Engineers", Engineers);
          return Engineers.ToString();
        case "grids":
          Int32.TryParse(value, out Grids);
          MyAPIGateway.Utilities.SetVariable<int>("SC-Grids", Grids);
          return Grids.ToString();
        case "difficulty":
          float.TryParse(value, out Difficulty);
          MyAPIGateway.Utilities.SetVariable<float>("SC-Difficulty", Difficulty);
          return Difficulty.ToString();
      }
      return 'null';
    }

    public string Get( string convar ) {
      switch( convar.ToLower() ) {
        case "engineers": return Engineers.ToString();
        case "grids": return Grids.ToString();
        case "difficulty": return Difficulty.ToString();
      }
      return 'null';
    }


  }

}
