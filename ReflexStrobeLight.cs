//* "This work uses content from the Sansar Knowledge Base. © 2017 Linden Research, Inc. Licensed under the Creative Commons Attribution 4.0 International License (license summary available at https://creativecommons.org/licenses/by/4.0/ and complete license terms available at https://creativecommons.org/licenses/by/4.0/legalcode)."
using System;
using System.Collections.Generic;
using Sansar;
using Sansar.Script;
using Sansar.Simulation;
using System.Diagnostics;

namespace Reflex4_StrobeLight
{

    [RegisterReflective]
    public class SceneOdbject : SceneObjectScript, IReflexScript, IReflexScriptHandshake
    {
        public string ScriptName { get { return "Reflex4 my scene"; } }
        public int ScriptVersion { get { return 2; } }
        public int RequiresHubVersion { get { return 12; } }

        #region Public settings


        [DisplayName("ReflexName")]
        [DefaultValue("MyScene")]
        public string _ReflexName;

        public bool SsOutputBangs;

        [DefaultValue("TrigVol1:On, Click1")]
        public string SampleBangs;

        [DefaultValue(true)]
        public bool SampleBoolSetting;

        [DefaultValue(12.3)]
        [Range(-10, 20)]
        public float SampleFloatSetting;

        [DefaultValue("http://mm-reflex.com/v4/MyScene")]
        public string GetHelpOnline;

        [DisplayName("OverrideSettings")]
        public string _OverrideSettings;


        [DisplayName("MainBodyIndex")]
        private int _MainBodyIndex = 0;


        #endregion

        #region Your customized Reflex methods


        /// <summary> Every Sansar script starts off with this method </summary>
        public override void Init()
        {
            Reflex_Initialize();  // Always start with this
            // Subscribe to various update only if you have specific uses. And then use 
            // the .RealtimeUpdateEnabled and .LowPriorityUpdateEnabled values to switch 
            // into low-resource modes when nothing is happening. This is key to how 
            // Reflex4 conserves CPU resources.

            /*
            ReflexHub.Subscribe_RealtimeUpdate(this, () => {
                Reflex_RealtimeUpdate();
            });
            RealtimeUpdateEnabled = false;

            ReflexHub.Subscribe_LowPriorityUpdate(this, () => {
                Reflex_LowPriorityUpdate();
            });
            LowPriorityUpdateEnabled = false;

            ReflexHub.Subscribe_MaintenanceUpdate(this, () => {
                Reflex_MaintenanceUpdate();
            });

            ReflexHub.Subscribe_VisitorChanged(this, (VisitorObj, ChangeType) => {
                IVisitor Visitor = VisitorObj.AsInterface<IVisitor>();
                Reflex_VisitorChanged(Visitor, ChangeType);
            });
            */

        }


        /// <summary> This is the second wave of initialization, once all scripts register </summary>
        public void Reflex_StartUp()
        {
            StrobeLightInit();
            // Be sure to hard-code bang routes here and not in .Init()
            //ReflexHub.SetBangRoutes(this, "SomeOtherRoute", "TrigVol1:Off, Click2");
     
            //ReflexHub.SetSettings(this, "RocketNudge", @"TriggerBangs: RocketBooster LinearVelocity: 0, 0, 40000 ");

        }


        /// <summary> The hub or other scripts may get my current public settings </summary>
        public void Reflex_GetSettings(Dictionary<string, string> Values)
        {
            Values["ReflexName"] = ReflexName;
            Values["SsOutputBangs"] = (SsOutputBangs ? "Yes" : "No");
            Values["SampleBangs"] = SampleBangs;
            Values["SampleBoolSetting"] = (SampleBoolSetting ? "Yes" : "No");
            Values["SampleFloatSetting"] = SampleFloatSetting.ToString("0.###");
        }


        /// <summary> The hub or other scripts may change my public settings </summary>
        public void Reflex_SetSettings(Dictionary<string, string> Values, List<string> Errors, AgentPrivate ForAgt)
        {
            foreach (var KeyValue in Values)
            {
                try
                {
                    string Value = ("" + KeyValue.Value).Trim();
                    switch (KeyValue.Key.ToLower())
                    {
                        // Make sure the case statements below are all lower case

                        case "reflexname":
                            //ReflexName = Value;  // For the hub this is a constant
                            break;

                        case "ssoutputbangs":
                            SsOutputBangs = Util.ToBool(Value);
                            ReflexHub12.EnableBangOutputToSimpleScripts(this, SsOutputBangs, null);
                            break;

                        case "samplebangs":
                            SampleBangs = "StrobeOn, StrobeOff ";
                            ReflexHub.SetBangRoutes(this, "Strobe", SampleBangs); //Route and Bang List
                            break;

                        case "sampleboolsetting":
                            SampleBoolSetting = Util.ToBool(Value);
                            break;

                        case "samplefloatsetting":
                            SampleFloatSetting = Util.ToFloat(Value);
                            break;

                        case "mainbodyindex":
                            ReflexHub.SelectMainBody(this, Util.ToInt(Value));
                            break;

                        default:
                            Errors.Add("No such setting named '" + KeyValue.Key + "'");
                            break;

                    }
                }
                catch (Exception Ex)
                {
                    throw new Exception(".Reflex_SetSettings(): '" + KeyValue.Key + "' key: " + Ex.Message, Ex);
                }
            }

            // Validate the settings
            if (SampleBoolSetting && SampleFloatSetting < 0) Errors.Add("SampleFloatSetting is below zero when SampleBoolSetting is on; This is realy bad for some reason");

        }


        /// <summary> Allows console and other scripts to execute custom operations </summary>
        public Dictionary<string, string> Reflex_Do(string OperationName, Dictionary<string, string> Args = null)
        {
            Dictionary<string, string> Ret = new Dictionary<string, string>();
            switch (OperationName)
            {

                case "SomeAwesomeOperation":
                    var X = "dude";
                    if (Args.ContainsKey("X")) X = ("" + Args["X"]).Trim();
                    Ret["A"] = "123";
                    Ret["B"] = "Whatever, " + X;
                    ScenePrivate.Chat.MessageAllUsers("Awesome thingy engaged");
                    break;

                default:
                    throw new Exception("No such operation named '" + OperationName + "'");

            }
            return Ret;
        }


        /// <summary> Respond to some other Reflex script sending a bang message I'm listening for </summary>
        public void Reflex_IncomingBang(Reflective BangMessageObj)
        {
            IBangMessage Msg = BangMessageObj.AsInterface<IBangMessage>();
            switch (Msg.RouteName)
            {

                case "Strobe":
                    ScenePrivate.Chat.MessageAllUsers("Strobe bang received.");
                    StrobeReflexHandler(Msg);
                    break;

            }
        }


        /// <summary> All scripts can opt to update in a synchronized wave </summary>
        public void Reflex_RealtimeUpdate()
        {
            /*
            Vector MyPos = ObjectPrivate.InitialPosition;
            Quaternion MyOri = ObjectPrivate.InitialRotation;
            if (MainBody != null) {
                MyPos = MainBody.GetPosition();
                MyOri = MainBody.GetOrientation();
            }
            */
        }


        /// <summary> Slower than typical realtime animation updates </summary>
        public void Reflex_LowPriorityUpdate()
        {

        }


        /// <summary> Very low frequency updates for occasional maintenance </summary>
        public void Reflex_MaintenanceUpdate()
        {
        }


        /// <summary> A visitor has arrived, left, etc. </summary>
        public void Reflex_VisitorChanged(IVisitor Visitor, int ChangeType)
        {
            switch (ChangeType)
            {

                case 1:  // Starting to arrive
                    Log.Write(Visitor.Agt.AgentInfo.Name + " is starting to arrive");
                    break;

                case 2:  // Done arriving
                    Log.Write(Visitor.Agt.AgentInfo.Name + " has arrived");
                    break;

                case 3:  // Left
                    Log.Write(Visitor.Agt.AgentInfo.Name + " has left");
                    break;

                case 4:  // Going AFK
                    Log.Write(Visitor.Agt.AgentInfo.Name + " has gone AFK");
                    break;

                case 5:  // Back from AFK
                    Log.Write(Visitor.Agt.AgentInfo.Name + " is back from AFK");
                    break;

            }
        }

        #endregion

        #region Private implementation
        public int CyclesPerSecond = 1;
        public Sansar.Color LightColor;
        public float LightIntensity = 10;

        private LightComponent Light = null;
        private bool StrobeLightOn = false;

        public void StrobeLightInit()
        {
            if (Light == null)
            {
                if (!ObjectPrivate.TryGetFirstComponent(out Light))
                {
                    ScenePrivate.Chat.MessageAllUsers("There is no Light Component attached to this object.");
                    return;
                }
            }

           // ScenePrivate.Chat.Subscribe(0, null, GetChatCommand);

        }

        private void StrobeReflexHandler(IBangMessage BangMsg)
        {
            if (Light != null)
            {
                if (BangMsg.Name.Contains("Strobe:On"))
                {
                    StrobeLightOn = true;
                    StartCoroutine(StrobeLight);
                }
                if (BangMsg.Name.Contains("Strobe:Off"))
                {
                    StrobeLightOn = false;
                }
            }
        }

        private void StrobeLight()
        {
            Log.Write("In Strobe Light");
            Log.Write("StrobeLightOn: " + StrobeLightOn);
            float CycleRate = CyclesPerSecond / 60 * 100;
            Log.Write("CycleRate: " + CycleRate);
            do
            {
                Log.Write("LightColor: " + LightColor);
                Light.SetColorAndIntensity(LightColor, LightIntensity);

                Wait(TimeSpan.FromMilliseconds(CycleRate / 2));
                Light.SetColorAndIntensity(LightColor, 0.0f);
                Wait(TimeSpan.FromMilliseconds(CycleRate / 2));
            } while (StrobeLightOn);
        }
        #endregion

        //---- Begin Reflex 4 library code ----//
        #region Reflex 4 scene object stuff (do not change)


        // This allows the hub to validate its compatibility with me
        public int IReflexScriptVersion { get { return 1; } }

        private IReflexHub ReflexHub;

        private IReflexHub12 ReflexHub12;

        public Reflective AsReflective { get { return this; } }

        public ScriptHandle ScriptBase_Script { get { return Script; } }

        public ObjectPrivate ScriptBase_ObjectPrivate { get { return ObjectPrivate; } }

        public string ReflexName
        {
            get { return _ReflexName; }
            set
            {
                string OldName = _ReflexName;
                _ReflexName = value;
                if (ReflexHub == null) return;
                ReflexHub.ReflexNameChanged(this, OldName);
            }
        }

        public bool Enabled { get; set; }

        public string OverrideSettings { get { return _OverrideSettings; } }

        public int MainBodyIndex
        {
            get { return _MainBodyIndex; }
            set { _MainBodyIndex = value; }
        }

        public RigidBodyComponent MainBody { get; set; }

        public bool RealtimeUpdateEnabled { get; set; }

        public bool LowPriorityUpdateEnabled { get; set; }

        private DateTime ScriptLoadedAt = DateTime.Now;


        public void Reflex_Initialize()
        {

            IReflexHubHandshake HubHandshake = null;

            while (true)
            {

                try
                {
                    // Hopefully the hub is out there waiting to hear from all of us Reflex scripts
                    foreach (IReflexHubHandshake Hub in ScenePrivate.FindReflective<IReflexHubHandshake>("Reflex4_Hub.SceneObject"))
                    {
                        HubHandshake = Hub;
                        break;
                    }
                }
                catch (Exception Ex)
                {
                    throw new Exception("Error in '" + ReflexName + "' (" + ScriptName + ") while searching for Reflex hub: " + Ex.Message, Ex);
                }

                if (HubHandshake != null) break;  // Found it

                // If not, we'll keep retrying for a while in case the hub still needs time to initialize
                if (DateTime.Now.Subtract(ScriptLoadedAt).TotalSeconds > 5f)
                {
                    throw new Exception("Error in '" + ReflexName + "' (" + ScriptName + "): Reflex hub script not found; I won't work at all");
                }

                Wait(TimeSpan.FromMilliseconds(100));
            }

            try
            {
                if (!HubHandshake.RegisterScript(this)) return;
            }
            catch (Exception Ex)
            {
                throw new Exception("Error in '" + ReflexName + "' (" + ScriptName + "): ReflexHub.RegisterScript(): " + Ex.Message, Ex);
            }

            Util.Rnd = new Random(ReflexHub.GetRandomSeed());

        }

        public void SetReflexHub(Reflective ReflexHubObj)
        {
            this.ReflexHub = ReflexHubObj.AsInterface<IReflexHub>();
            this.ReflexHub12 = ReflexHubObj.AsInterface<IReflexHub12>();
        }


        #endregion

    }

    #region Reflex 4 library (do not change)

    public class AnchorPointType
    {
        public const int Head = 0;
        public const int Body = 1;
        public const int Child = 2;
        public static int MaxValue = 2;
        public static string NameOf(int Value)
        {
            switch (Value)
            {
                case 0: return "Head";
                case 1: return "Body";
                case 2: return "Child";
                default: return "<unknown>";
            }
        }
    }


    public class AvatarPartOption
    {
        public const int Body = 0;
        public const int Head = 1;
        public const int LeftHand = 2;
        public const int RightHand = 3;
        public static int MaxValue = 3;
        public static string NameOf(int Value)
        {
            switch (Value)
            {
                case 0: return "Body";
                case 1: return "Head";
                case 2: return "LeftHand";
                case 3: return "RightHand";
                default: return "<unknown>";
            }
        }
    }


    public interface IReflexScriptHandshake
    {
        #region Public members

        Reflective AsReflective { get; }
        string ScriptName { get; }
        int ScriptVersion { get; }
        int RequiresHubVersion { get; }
        int IReflexScriptVersion { get; }
        string ReflexName { get; set; }
        bool Enabled { get; set; }

        void SetReflexHub(Reflective ReflexHubObj);

        #endregion
    }


    public interface IReflexScript
    {
        #region Public members

        bool RealtimeUpdateEnabled { get; set; }
        bool LowPriorityUpdateEnabled { get; set; }

        Reflective AsReflective { get; }
        string ScriptName { get; }
        int ScriptVersion { get; }
        int RequiresHubVersion { get; }
        ScriptHandle ScriptBase_Script { get; }
        ObjectPrivate ScriptBase_ObjectPrivate { get; }
        string ReflexName { get; set; }
        bool Enabled { get; set; }
        string OverrideSettings { get; }
        int MainBodyIndex { get; set; }
        RigidBodyComponent MainBody { get; set; }

        void SetReflexHub(Reflective HubObj);
        void Reflex_StartUp();
        void Reflex_GetSettings(Dictionary<string, string> Values);
        void Reflex_SetSettings(Dictionary<string, string> Values, List<string> Errors, AgentPrivate ForAgt);
        Dictionary<string, string> Reflex_Do(string OperationName, Dictionary<string, string> Args = null);
        void Reflex_IncomingBang(Reflective BangMessageObj);

        #endregion
    }


    public interface IReflexHubHandshake
    {
        #region Public members

        Reflective AsReflective { get; }
        string ScriptName { get; }
        int ScriptVersion { get; }
        bool RegisterScript(Reflective ScriptObj);

        #endregion
    }


    public interface IReflexHub
    {
        #region Public members

        Reflective AsReflective { get; }
        bool StartedUp { get; }
        float RealtimeUpdateFps { get; }
        Vector StowagePosition { get; }

        void ReflexNameChanged(Reflective ScriptObj, string OldName);
        void SetBangRoutes(Reflective FromScriptObj, string RouteName, string BangNamesCsv);
        void SetSettings(Reflective FromScriptObj, string ScriptNameOrId, string Settings);
        void Bang(Reflective ScriptObj, string Name);
        void Bang(Reflective ScriptObj, string Name, float Number);
        void Bang(Reflective ScriptObj, string Name, AgentPrivate Agt);
        void Bang(Reflective ScriptObj, string Name, AgentPrivate Agt, float Number);
        void Bang(Reflective ScriptObj, string Name, ObjectPrivate Obj);
        void Bang(Reflective ScriptObj, string Name, ObjectPrivate Obj, float Number);
        void Bang(Reflective ScriptObj, ScriptId ToScriptId, string Name, AgentPrivate Agt, ObjectPrivate Obj,
            float Number, string Text, Vector Position, Quaternion Orientation, Color Color);
        void Bang(Reflective ScriptObj, Reflective BangMsgObj);
        void Subscribe_VisitorChanged(Reflective ScriptObj, Action<Reflective, int> Handler);
        void Subscribe_RealtimeUpdate(Reflective ScriptObj, Action Handler);
        void Subscribe_LowPriorityUpdate(Reflective ScriptObj, Action Handler);
        void Subscribe_MaintenanceUpdate(Reflective ScriptObj, Action Handler);
        bool CanAdmin(AgentPrivate Agt);

        [Obsolete]
        long SendWebRequest(string Operation, Dictionary<string, string> Args, Action<long, string, Dictionary<string, string>> WebResponse);

        long SendWebRequest(Reflective ScriptObj, string Operation, Dictionary<string, string> Args, Action<long, string, Dictionary<string, string>> WebResponse);
        void ResetScene();
        void ShowMenu2Option(Reflective MenuObj);
        void SelectMainBody(Reflective ScrObj, int BodyIndex);
        Reflective AddAnchorPoint(
            Reflective ScrObj,
            int Type,  // 0: My anchor | 1: Base | 2: Anchor point
            string Name,
            RigidBodyComponent Body,
            int OffsetFromParentId,
            Vector OffsetPosition,
            Quaternion OffsetOrientation
        );
        List<Reflective> GetScriptsByName(string ReflexName);
        int GetRandomSeed();
        string Signature(Reflective ScriptObj, string Text);
        void HandleException(Reflective ScriptObj, Exception Ex);

        #endregion
    }


    public interface IReflexHub12
    {
        #region Public members

        void EnableBangOutputToSimpleScripts(Reflective ScriptObj, bool Enabled, List<string> ExcludeBangs);

        #endregion
    }


    public interface IBangMessage
    {
        #region Public members

        Reflective AsReflective { get; }
        Reflective FromScriptObj { get; }
        ScriptId ToScriptId { get; set; }
        string RouteName { get; set; }
        string Name { get; set; }
        AgentPrivate Agt { get; set; }
        ObjectPrivate Obj { get; set; }
        float Number { get; set; }
        string Text { get; set; }
        Vector Position { get; set; }
        Quaternion Orientation { get; set; }
        Color Color { get; set; }

        #endregion
    }


    public class BangMessage : Reflective, IBangMessage
    {
        #region Public interface

        public Reflective AsReflective { get { return this; } }
        public Reflective FromScriptObj { get; internal set; }
        public ScriptId ToScriptId { get; set; } = ScriptId.Invalid;
        public string RouteName { get; set; }
        public string Name { get; set; }
        public AgentPrivate Agt { get; set; }
        public ObjectPrivate Obj { get; set; }
        public float Number { get; set; }
        public string Text { get; set; } = "";
        public Vector Position { get; set; } = Vector.Zero;
        public Quaternion Orientation { get; set; } = Quaternion.Identity;
        public Color Color { get; set; } = Color.Black;

        public BangMessage(Reflective FromScriptObj)
        {
            this.FromScriptObj = FromScriptObj;
        }

        public BangMessage(Reflective FromScriptObj, IBangMessage FromBang)
        {
            this.FromScriptObj = FromScriptObj;
            ToScriptId = FromBang.ToScriptId;
            RouteName = FromBang.RouteName;
            Name = FromBang.Name;
            Agt = FromBang.Agt;
            Obj = FromBang.Obj;
            Number = FromBang.Number;
            Text = FromBang.Text;
            Position = FromBang.Position;
            Orientation = FromBang.Orientation;
        }

        #endregion
    }


    public interface IAnchorPoint
    {
        #region Public members

        Reflective AsReflective { get; }
        int Id { get; }
        int Type { get; }  // 0: Head | 1: Body | 2: Child
        string Name { get; }
        bool AttachedToWorld { get; }
        RigidBodyComponent AttachedToBody { get; }
        AgentPrivate AttachedToAvatar { get; }
        ObjectPrivate AttachedToAvatar_Object { get; set; }
        AnimationComponent AttachedToAvatar_Animation { get; set; }
        int AvatarPart { get; set; }  // 0: Body | 1: Head | 2: Left hand | 3: Right hand
        Vector OffsetPosition { get; set; }
        Quaternion OffsetOrientation { get; set; }
        bool InMotion { get; set; }
        bool IsAttached { get; }
        Vector Position { get; set; }
        Quaternion Orientation { get; set; }
        Vector LinearVelocity { get; set; }
        Vector AngularVelocity { get; set; }

        void Delete();
        void AttachToWorld();
        void AttachToPoint(int Id);
        void AttachToPoint(string Name);
        void AttachToBody(RigidBodyComponent Body);
        void AttachToAvatar(AgentPrivate Agt);
        void Detach();
        void Freshen();
        void Update();

        #endregion
    }


    public interface IVisitor
    {
        #region Public members

        Reflective AsReflective { get; }
        AgentPrivate Agt { get; }
        ObjectPrivate Obj { get; }
        AnimationComponent Anim { get; }
        DateTime ArrivedAt { get; }
        DateTime LeftAt { get; }
        bool Afk { get; }
        Guid GlobalSessionId { get; }

        #endregion
    }


    public class ReflexMenu2Option : Reflective
    {
        #region Public interface

        public delegate void HandlerDelegate();

        public IReflexHub ReflexHub;

        public AgentPrivate Agt { get; set; }

        public bool Interrupt { get; set; } = false;

        public string Message { get; set; } = "Your message here";

        public string PositiveOptionCaption { get; set; } = "Okay";

        public string NegativeOptionCaption { get; set; }

        public HandlerDelegate FailureHandler;

        public HandlerDelegate PositiveOptionHandler;

        public HandlerDelegate NegativeOptionHandler;

        public ReflexMenu2Option(IReflexHub ReflexHub, AgentPrivate Agt)
        {
            this.ReflexHub = ReflexHub;
            this.Agt = Agt;
        }

        public ReflexMenu2Option(IReflexHub ReflexHub, AgentPrivate Agt, string Message)
        {
            this.ReflexHub = ReflexHub;
            this.Agt = Agt;
            this.Message = Message;
        }

        public void Show()
        {
            ReflexHub.ShowMenu2Option(this);
        }

        public void HandleEvent(bool Failure, bool PositiveOptionChosen)
        {
            if (Failure)
            {
                if (FailureHandler != null) FailureHandler();
            }
            else if (PositiveOptionChosen)
            {
                if (PositiveOptionHandler != null) PositiveOptionHandler();
            }
            else
            {
                if (NegativeOptionHandler != null) NegativeOptionHandler();
            }
        }

        #endregion
    }


    /// <summary> General purpose shared methods </summary>
    public class Util
    {
        #region Public static methods


        public static Random Rnd;


        /// <summary> Limited URL-encoding of plain text to add as data values in query strings </summary>
        public static string ToUrl(string Text)
        {
            return Text
                .Replace("%", "%25")
                .Replace("&", "%26")
                .Replace("+", "%2B")
                .Replace("'", "%27")
                .Replace("\"", "%22")
                .Replace("{", "%7B")
                .Replace("}", "%7D")
                .Replace(" ", "%20")
            ;
        }


        /// <summary> Limited encoding of plain text to JavaScript string literals </summary>
        public static string ToJavaScriptStringLiteral(string Text)
        {
            return Text
                .Replace(@"\", @"\\")
                .Replace("\"", @"\""")
                .Replace("'", @"\'")
                .Replace("\0x0000", @"\0")
                .Replace("\r", @"\r")
                .Replace("\n", @"\n")
                .Replace("\t", @"\t")
            ;
        }


        /// <summary> Clamp the value to fit within the given range </summary>
        public static float Clamp(float Value, float Min, float Max)
        {
            if (Value < Min) return Min;
            if (Value > Max) return Max;
            return Value;
        }


        /// <summary> Clamp the value to fit within the given range </summary>
        public static int Clamp(int Value, int Min, int Max)
        {
            if (Value < Min) return Min;
            if (Value > Max) return Max;
            return Value;
        }


        /// <summary> Clamp the value to fit within the given range </summary>
        public static float ClampMin(float Value, float Min)
        {
            if (Value < Min) return Min;
            return Value;
        }


        /// <summary> Clamp the value to fit within the given range </summary>
        public static int ClampMin(int Value, int Min)
        {
            if (Value < Min) return Min;
            return Value;
        }


        /// <summary> Clamp the value to fit within the given range </summary>
        public static float ClampMax(float Value, float Max)
        {
            if (Value > Max) return Max;
            return Value;
        }


        /// <summary> Clamp the value to fit within the given range </summary>
        public static int ClampMax(int Value, int Max)
        {
            if (Value > Max) return Max;
            return Value;
        }


        /// <summary> Parse a boolean value out of the given text </summary>
        public static bool ToBool(string Text)
        {
            Text = ("" + Text).Trim().ToLower();
            switch (Text)
            {
                case "true":
                case "yes":
                case "on":
                case "1":
                case "-1":
                case "t":
                case "y":
                    return true;
                default:
                    return false;
            }
        }


        /// <summary> Parse the given floating point number </summary>
        public static float ToFloat(string Text)
        {
            string[] Parts = Text.Split('/');
            if (Parts.Length == 1)
            {
                return float.Parse(Text);
            }
            else if (Parts.Length == 2)
            {
                // Can be in a fractional form (e.g., "1/3")
                return float.Parse(Parts[0]) / float.Parse(Parts[1]);
            }
            return 0;
        }


        /// <summary> Parse the given floating point number </summary>
        public static int ToInt(string Text)
        {
            int Value;
            if (int.TryParse(Text, out Value)) return Value;
            return (int)Math.Round(ToFloat(Text));
        }


        /// <summary> Parse the given vector </summary>
        public static Vector ToVector(string Text)
        {
            if (Text == null) return Vector.Zero;
            Text = Text.Replace("<", "").Replace(">", "");
            string[] Parts = Text.Split(',');
            if (Parts.Length == 3)
            {
                return new Vector(
                    Util.ToFloat(Parts[0]),
                    Util.ToFloat(Parts[1]),
                    Util.ToFloat(Parts[2])
                );
            }
            else if (Parts.Length == 2)
            {
                return new Vector(
                    Util.ToFloat(Parts[0]),
                    Util.ToFloat(Parts[1]),
                    0
                );
            }
            else
            {
                return Vector.Zero;
            }
        }


        /// <summary> Transform a vector into text (e.g., "27.154, 13, 0.001") </summary>
        public static string FromVector(Vector Value, int SignificantDigits = 3)
        {
            if (SignificantDigits < 0)
            {
                return
                    Value.X + "," +
                    Value.Y + "," +
                    Value.Z;
            }

            string DecimalFormat = "0.";
            for (int I = 0; I < SignificantDigits; I++) DecimalFormat += "#";
            return
                Value.X.ToString(DecimalFormat) + "," +
                Value.Y.ToString(DecimalFormat) + "," +
                Value.Z.ToString(DecimalFormat);
        }


        /// <summary> Parse out a rotation that could be represnted as four-number 
        /// quaternion, a three-number Euler angles vector, or a one-number angle 
        /// representing a rotation about the Z axis </summary>
        public static Quaternion ToQuaternion(string Text)
        {
            if (Text == null) return Quaternion.Identity;
            Text = Text.Replace("<", "").Replace(">", "");
            string[] Parts = Text.Split(',');

            if (Parts.Length == 4)
            {
                // A pure quaternion
                return new Quaternion(
                    Util.ToFloat(Parts[0]),
                    Util.ToFloat(Parts[1]),
                    Util.ToFloat(Parts[2]),
                    Util.ToFloat(Parts[3])
                ).Normalized();

            }
            else if (Parts.Length == 3)
            {
                // A set of 3 Euler angles, expressed in degrees
                return Quaternion.FromEulerAngles(
                    new Vector(
                        Util.ToFloat(Parts[0]),
                        Util.ToFloat(Parts[1]),
                        Util.ToFloat(Parts[2])
                    ) * Mathf.RadiansPerDegree
                );

            }
            else if (Parts.Length == 1)
            {
                // A single 2D rotation about the Z axis, expressed in degrees
                return Quaternion.FromEulerAngles(
                    new Vector(
                        0,
                        0,
                        Util.ToFloat(Parts[0]) * Mathf.RadiansPerDegree
                    )
                );

            }
            else
            {
                return Quaternion.Identity;

            }

        }


        public static string FromQuaternion(Quaternion Value, bool AsEulerAngles = true, int SignificantDigits = 3)
        {

            if (AsEulerAngles)
            {
                return FromVector(Value.GetEulerAngles() * Mathf.DegreesPerRadian);
            }

            if (SignificantDigits < 0)
            {
                return
                    Value.X + "," +
                    Value.Y + "," +
                    Value.Z + "," +
                    Value.W;
            }

            string DecimalFormat = "0.";
            for (int I = 0; I < SignificantDigits; I++) DecimalFormat += "#";
            return
                Value.X.ToString(DecimalFormat) + "," +
                Value.Y.ToString(DecimalFormat) + "," +
                Value.Z.ToString(DecimalFormat) + "," +
                Value.W.ToString(DecimalFormat);
        }


        /// <summary> Parse the given color </summary>
        public static Color ToColor(string Text)
        {
            if (Text == null) return Color.Black;
            Text = Text.Replace("(", "").Replace(")", "");
            string[] Parts = Text.Split(',');
            if (Parts.Length == 4)
            {
                return new Color(
                    Clamp(ToFloat(Parts[0]), 0, 255) / 255f,
                    Clamp(ToFloat(Parts[1]), 0, 255) / 255f,
                    Clamp(ToFloat(Parts[2]), 0, 255) / 255f,
                    Clamp(ToFloat(Parts[3]), 0, 255) / 255f
                );
            }
            else if (Parts.Length == 3)
            {
                return new Color(
                    Clamp(ToFloat(Parts[0]), 0, 255) / 255f,
                    Clamp(ToFloat(Parts[1]), 0, 255) / 255f,
                    Clamp(ToFloat(Parts[2]), 0, 255) / 255f
                );
            }
            else
            {
                return Color.Black;
            }
        }


        /// <summary> Transform a color value into text (e.g., "27.154, 13, 0.001") </summary>
        public static string FromColor(Color Value, int SignificantDigits = 2)
        {
            if (SignificantDigits < 0)
            {
                return
                    (Value.R * 255).ToString("0") + "," +
                    (Value.G * 255).ToString("0") + "," +
                    (Value.B * 255).ToString("0") + (
                        Value.A >= 1 ?
                        "" :
                        "," + (Value.A * 255).ToString("0")
                    );
            }

            string DecimalFormat = "0.";
            for (int I = 0; I < SignificantDigits; I++) DecimalFormat += "#";
            return
                (Value.R * 255).ToString(DecimalFormat) + "," +
                (Value.G * 255).ToString(DecimalFormat) + "," +
                (Value.B * 255).ToString(DecimalFormat) + (
                    (Value.A >= 1 ?
                    "" :
                    "," + (Value.A * 255).ToString(DecimalFormat)
                ));
        }


        public static Quaternion Inverse(Quaternion Value)
        {
            float Num1 = (((Value.X * Value.X) + (Value.Y * Value.Y)) + (Value.Z * Value.Z)) + (Value.W * Value.W);
            float Num = 1f / Num1;
            return new Quaternion(
                -Value.X * Num,
                -Value.Y * Num,
                -Value.Z * Num,
                 Value.W * Num
            );
        }


        public static Quaternion Add(Quaternion A, Quaternion B)
        {
            return new Quaternion(
                A.X + B.X,
                A.Y + B.Y,
                A.Z + B.Z,
                A.W + B.W
            );
        }


        public static Quaternion Subtract(Quaternion A, Quaternion B)
        {
            return new Quaternion(
                A.X - B.X,
                A.Y - B.Y,
                A.Z - B.Z,
                A.W - B.W
            );
        }


        public static Quaternion Multiply(Quaternion A, float B)
        {
            return new Quaternion(
                A.X * B,
                A.Y * B,
                A.Z * B,
                A.W * B
            );
        }


        public static bool WithinSphere(Vector Position, Vector Center, float Radius)
        {
            return (Position - Center).Length() <= Radius;
        }


        public static float PlusOrMinus(float Range)
        {
            return (float)(Rnd.NextDouble() * Range * 2 - Range);
        }


        /// <summary> Quadratic ease in quadratic </summary>
        public static float EaseIn(float Percent)
        {
            return Percent * Percent;
        }


        /// <summary> Quadratic ease out </summary>
        public static float EaseOut(float Percent)
        {
            return Percent * (2 - Percent);
        }


        /// <summary> Quadratic ease in and then out </summary>
        public static float EaseInOut(float Percent)
        {
            float Squared = Percent * Percent;
            return Squared / (2.0f * (Squared - Percent) + 1.0f);
        }


        /// <summary> Transforms an axis of rotation and an angle to rotate by into a quaternion </summary>
        public static Quaternion AngleAxis(float Angle, Vector Axis)
        {
            float RotAngleDiv2 = Angle / 2f;
            Vector AxisNorm = Axis.Normalized();
            return new Quaternion(
                (float)(AxisNorm.X * Math.Sin(RotAngleDiv2)),
                (float)(AxisNorm.Y * Math.Sin(RotAngleDiv2)),
                (float)(AxisNorm.Z * Math.Sin(RotAngleDiv2)),
                (float)Math.Cos(RotAngleDiv2)
            );
        }


        /// <summary> Estimates the constant angular velocity given two rotations in time </summary>
        public static Vector AngularVelocity(Quaternion From, Quaternion To, float DeltaSeconds)
        {

            float Dot = From.Dot(ref To);

            // If the inputs are too close for comfort, assume it's not rotating at all
            if (Math.Abs(Dot) > 0.9995) return Vector.Zero;

            // If the dot product is negative, the quaternions
            // have opposite handed-ness and we won't take
            // the shorter path. Fix by reversing one quaternion.
            if (Dot < 0)
            {
                From = From.Inverse();
                // From = -From;  // To do: Verify that the new way is equivalent
                Dot = -Dot;
            }

            if (Dot > 1) Dot = 1;  // Robustness: Stay within domain of acos()
            float Theta = (float)Math.Acos(Dot);  // theta_0 = angle between input vectors

            return Vector.Up.Rotate(ref From) * Theta / DeltaSeconds;

        }


        /// <summary> Computes a spherical linear interpolation (SLERP) between two orientations </summary>
        public static Quaternion Slerp(Quaternion From, Quaternion To, float Percent)
        {
            // Adapted from:  https://en.wikipedia.org/wiki/Slerp

            float Dot = From.Dot(ref To);

            // If the inputs are too close for comfort, linearly interpolate
            // and normalize the result.
            if (Math.Abs(Dot) > 0.9995)
            {
                return Add(
                    From,
                    Multiply(
                        Subtract(To, From),
                        Percent
                    )
                ).Normalized();
            }

            // If the dot product is negative, the quaternions
            // have opposite handed-ness and slerp won't take
            // the shorter path. Fix by reversing one quaternion.
            if (Dot < 0)
            {
                From = From.Inverse();
                // From = -From;  // To do: Verify that the new way is equivalent
                Dot = -Dot;
            }

            if (Dot > 1) Dot = 1;  // Robustness: Stay within domain of acos()
            double Theta = Percent * Math.Acos(Dot);  // theta_0 = angle between input vectors

            Quaternion Target = Subtract(
                To,
                Multiply(From, Dot)
            ).Normalized();

            return Add(
                Multiply(From, (float)Math.Cos(Theta)),
                Multiply(Target, (float)Math.Sin(Theta))
            );

        }


        /// <summary> A very basic text encoder for packing richer text within 
        /// multi-value text strings </summary>
        public static string Escape(string Text)
        {
            Text = Text.Replace("\\", "\\b");
            Text = Text.Replace("\r", "\\r");
            Text = Text.Replace("\n", "\\n");
            Text = Text.Replace("\t", "\\t");
            Text = Text.Replace("|", "\\p");
            Text = Text.Replace(",", "\\c");
            Text = Text.Replace(":", "\\o");
            Text = Text.Replace(".", "\\d");
            return Text;
        }


        /// <summary> Reverses the effect of the .Escape() method </summary>
        public static string Unescape(string Text)
        {
            Text = Text.Replace("\\r", "\r");
            Text = Text.Replace("\\n", "\n");
            Text = Text.Replace("\\t", "\t");
            Text = Text.Replace("\\p", "|");
            Text = Text.Replace("\\c", ",");
            Text = Text.Replace("\\o", ":");
            Text = Text.Replace("\\d", ".");
            Text = Text.Replace("\\b", "\\");
            return Text;
        }


        public static void TeleportWithinScene(ScenePrivate Scene, AgentPrivate Agt, Vector Position)
        {
            ObjectPrivate Obj = Scene.FindObject(Agt.AgentInfo.ObjectId);
            if (Obj == null) return;
            AnimationComponent AgentAnim;
            if (!Obj.TryGetFirstComponent(out AgentAnim)) return;
            AgentAnim.SetPosition(Position);
        }


        public static void TeleportToScene(AgentPrivate Agt, string AtlasUri)
        {
            // E.g.:  https://atlas.sansar.com/experiences/galen/clockworks-club
            if (("" + AtlasUri).Trim() == "") throw new Exception("No Atlas URI specified to teleport someone to");
            string[] Parts = AtlasUri.Split('/');
            Agt.Client.TeleportToLocation(Parts[4], Parts[5]);
        }


        public static bool AgentOrObjectInRange(
            ScenePrivate Scene, RigidBodyComponent Body,
            AgentPrivate Agt, ObjectPrivate Obj,
            bool RelativePosition, Vector NearPosition, float WithinRadius
        )
        {
            float Distance;
            return AgentOrObjectInRange(Scene, Body, Agt, Obj, RelativePosition, NearPosition, WithinRadius, out Distance);
        }


        public static bool AgentOrObjectInRange(
            ScenePrivate Scene, RigidBodyComponent Body,
            AgentPrivate Agt, ObjectPrivate Obj,
            bool RelativePosition, Vector NearPosition, float WithinRadius,
            out float Distance
        )
        {
            Distance = -1;
            if (Agt == null) return false;
            if (WithinRadius <= 0) return true;

            if (Agt != null && Obj == null)
            {
                Obj = Scene.FindObject(Agt.AgentInfo.ObjectId);
                if (Obj == null) return false;
            }

            Vector AgentPos = Obj.Position;

            if (RelativePosition && Body != null)
            {
                Quaternion Rot = Body.GetOrientation();
                NearPosition = Body.GetPosition() + NearPosition.Rotate(ref Rot);
            }

            if ((NearPosition - AgentPos).LengthSquared() > WithinRadius * WithinRadius) return false;
            Distance = (NearPosition - AgentPos).Length();
            return true;
        }


        /// <summary> Parse a string containing a pipe-delimited string into a list </summary>
        public static void DelimitedToList(
            string DelimitedText, char Separator, List<string> Values,
            bool ToLowerCase = true, bool TrimValues = true, bool IgnoreBlanks = true
        )
        {
            if ("" + DelimitedText == "") return;

            // Split on pipes (|)
            foreach (string Item in DelimitedText.Split(Separator))
            {
                string Value = Item;
                if (TrimValues) Value = Value.Trim();
                if (IgnoreBlanks && Value == "") continue;
                if (ToLowerCase) Value = Value.ToLower();
                Values.Add(Unescape(Value));
            }
        }


        /// <summary> Translate a list of strings into pipe-delimited string </summary>
        public static string ListToDelimited(List<string> Values, string Separator)
        {
            string Text = "";
            foreach (string Item in Values)
            {
                if (Text != "") Text += Separator;
                Text += Escape(Item);
            }
            return Text;
        }


        /// <summary> Parse a string containing a pipe-delimited list of "Key: Value" pairs into a dictionary </summary>
        public static void ParseSettings(string OverrideSettings, Dictionary<string, string> Values)
        {
            if ("" + OverrideSettings == "") return;

            // The settings could alternatively be represented as one key/value pair on each line
            OverrideSettings = OverrideSettings.Replace("\r\n", "\n").Replace("\n", "|");

            // Split on pipes (|) and then colons (:)
            foreach (string Item in OverrideSettings.Split('|'))
            {
                string Item2 = Item.Trim();
                if (Item2 == "") continue;
                string[] KeyValue = Item.Split(new char[] { ':' }, 2);
                if (KeyValue.Length == 1)
                {
                    throw new Exception("ParseSettings: Setting '" + Item2 + "' is missing a colon (:) followed by a value");
                }
                string Value = KeyValue[1].Trim();
                Value = Value.Replace("\\p", "|");
                Value = Value.Replace("\\r", "\r");
                Value = Value.Replace("\\n", "\n");
                Value = Value.Replace("\\t", "\t");
                Value = Value.Replace("\\b", "\\");
                Values[KeyValue[0].Trim()] = Value;
            }
        }


        #endregion
    }


    #endregion
    //---- End Reflex 4 library code ----//

}

