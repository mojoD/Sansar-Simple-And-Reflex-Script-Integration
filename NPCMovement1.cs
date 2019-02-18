/* This content is licensed under the terms of the Creative Commons Attribution 4.0 International License.
 * When using this content, you must:
 * •    Acknowledge that the content is from the Sansar Knowledge Base.
 * •    Include our copyright notice: "© 2017 Linden Research, Inc."
 * •    Indicate that the content is licensed under the Creative Commons Attribution-Share Alike 4.0 International License.
 * •    Include the URL for, or link to, the license summary at https://creativecommons.org/licenses/by-sa/4.0/deed.hi (and, if possible, to the complete license terms at https://creativecommons.org/licenses/by-sa/4.0/legalcode.
 * For example:
 * "This work uses content from the Sansar Knowledge Base. © 2017 Linden Research, Inc. Licensed under the Creative Commons Attribution 4.0 International License (license summary available at https://creativecommons.org/licenses/by/4.0/ and complete license terms available at https://creativecommons.org/licenses/by/4.0/legalcode)."
 */

using Sansar;
using Sansar.Script;
using Sansar.Simulation;
using System;
using System.Linq;
using System.Collections.Generic;

public class NPCMovement1 : SceneObjectScript
{
    #region EditorProperties
    // Start playing on these events. Can be a comma separated list of event names.
    public string MoveTo1 = null;
    public string MoveTo2 = null;
    public string MoveTo3 = null;
    public string MoveTo4 = null;
    public string MoveTo5 = null;
    public string MoveTo6 = null;
    public string MoveTo7 = null;
    public string MoveTo8 = null;
    public string MoveTo9 = null;
    public string MoveTo10 = null;
    public string MoveTo11 = null;
    public string MoveTo12 = null;
    public string MoveTo13 = null;
    public string MoveTo14 = null;
    public string MoveTo15 = null;
    public string MoveTo16 = null;
    public string MoveTo17 = null;
    public string MoveTo18 = null;
    public string EnableEvent = null;
    public string DisableEvent = null;

    #endregion

    #region SimpleHelpers v2
    // Update the region tag above by incrementing the version when updating anything in the region.

    // If a Group is set, will only respond and send to other SimpleScripts with the same Group tag set.
    // Does NOT accept CSV lists of groups.
    // To send or receive events to/from a specific group from outside that group prepend the group name with a > to the event name
    // my_group>on
    [DefaultValue("")]
    [DisplayName("Group")]
    public string Group = "";

    public interface ISimpleData
    {
        AgentInfo AgentInfo { get; }
        ObjectId ObjectId { get; }
        ObjectId SourceObjectId { get; }

        // Extra data
        Reflective ExtraData { get; }
    }

    public class SimpleData : Reflective, ISimpleData
    {
        public SimpleData(ScriptBase script) { ExtraData = script; }
        public AgentInfo AgentInfo { get; set; }
        public ObjectId ObjectId { get; set; }
        public ObjectId SourceObjectId { get; set; }

        public Reflective ExtraData { get; }
    }

    public interface IDebugger { bool DebugSimple { get; } }
    private bool __debugInitialized = false;
    private bool __SimpleDebugging = false;
    private string __SimpleTag = "";

    private string GenerateEventName(string eventName)
    {
        eventName = eventName.Trim();
        if (eventName.EndsWith("@"))
        {
            // Special case on@ to send the event globally (the null group) by sending w/o the @.
            return eventName.Substring(0, eventName.Length - 1);
        }
        else if (Group == "" || eventName.Contains("@"))
        {
            // No group was set or already targeting a specific group as is.
            return eventName;
        }
        else
        {
            // Append the group
            return $"{eventName}@{Group}";
        }
    }

    private void SetupSimple()
    {
        __debugInitialized = true;
        __SimpleTag = GetType().Name + " [S:" + Script.ID.ToString() + " O:" + ObjectPrivate.ObjectId.ToString() + "]";
        Wait(TimeSpan.FromSeconds(1));
        IDebugger debugger = ScenePrivate.FindReflective<IDebugger>("SimpleDebugger").FirstOrDefault();
        if (debugger != null) __SimpleDebugging = debugger.DebugSimple;
    }

    System.Collections.Generic.Dictionary<string, Func<string, Action<ScriptEventData>, Action>> __subscribeActions = new System.Collections.Generic.Dictionary<string, Func<string, Action<ScriptEventData>, Action>>();
    private Action SubscribeToAll(string csv, Action<ScriptEventData> callback)
    {
        if (!__debugInitialized) SetupSimple();
        if (string.IsNullOrWhiteSpace(csv)) return null;

        Func<string, Action<ScriptEventData>, Action> subscribeAction;
        if (__subscribeActions.TryGetValue(csv, out subscribeAction))
        {
            return subscribeAction(csv, callback);
        }

        // Simple case.
        if (!csv.Contains(">>"))
        {
            __subscribeActions[csv] = SubscribeToAllInternal;
            return SubscribeToAllInternal(csv, callback);
        }

        // Chaining
        __subscribeActions[csv] = (_csv, _callback) =>
        {
            System.Collections.Generic.List<string> chainedCommands = new System.Collections.Generic.List<string>(csv.Split(new string[] { ">>" }, StringSplitOptions.RemoveEmptyEntries));

            string initial = chainedCommands[0];
            chainedCommands.RemoveAt(0);
            chainedCommands.Add(initial);

            Action unsub = null;
            Action<ScriptEventData> wrappedCallback = null;
            wrappedCallback = (data) =>
            {
                string first = chainedCommands[0];
                chainedCommands.RemoveAt(0);
                chainedCommands.Add(first);
                if (unsub != null) unsub();
                unsub = SubscribeToAllInternal(first, wrappedCallback);
                Log.Write(LogLevel.Info, "CHAIN Subscribing to " + first);
                _callback(data);
            };

            unsub = SubscribeToAllInternal(initial, wrappedCallback);
            return unsub;
        };

        return __subscribeActions[csv](csv, callback);
    }

    private Action SubscribeToAllInternal(string csv, Action<ScriptEventData> callback)
    {
        Action unsubscribes = null;
        string[] events = csv.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (__SimpleDebugging)
        {
            Log.Write(LogLevel.Info, __SimpleTag, "Subscribing to " + events.Length + " events: " + string.Join(", ", events));
        }
        Action<ScriptEventData> wrappedCallback = callback;

        foreach (string eventName in events)
        {
            if (__SimpleDebugging)
            {
                var sub = SubscribeToScriptEvent(GenerateEventName(eventName), (ScriptEventData data) =>
                {
                    Log.Write(LogLevel.Info, __SimpleTag, "Received event " + GenerateEventName(eventName));
                    wrappedCallback(data);
                });
                unsubscribes += sub.Unsubscribe;
            }
            else
            {
                var sub = SubscribeToScriptEvent(GenerateEventName(eventName), wrappedCallback);
                unsubscribes += sub.Unsubscribe;
            }
        }
        return unsubscribes;
    }

    System.Collections.Generic.Dictionary<string, Action<string, Reflective>> __sendActions = new System.Collections.Generic.Dictionary<string, Action<string, Reflective>>();
    private void SendToAll(string csv, Reflective data)
    {
        if (!__debugInitialized) SetupSimple();
        if (string.IsNullOrWhiteSpace(csv)) return;

        Action<string, Reflective> sendAction;
        if (__sendActions.TryGetValue(csv, out sendAction))
        {
            sendAction(csv, data);
            return;
        }

        // Simple case.
        if (!csv.Contains(">>"))
        {
            __sendActions[csv] = SendToAllInternal;
            SendToAllInternal(csv, data);
            return;
        }

        // Chaining
        System.Collections.Generic.List<string> chainedCommands = new System.Collections.Generic.List<string>(csv.Split(new string[] { ">>" }, StringSplitOptions.RemoveEmptyEntries));
        __sendActions[csv] = (_csv, _data) =>
        {
            string first = chainedCommands[0];
            chainedCommands.RemoveAt(0);
            chainedCommands.Add(first);

            Log.Write(LogLevel.Info, "CHAIN Sending to " + first);
            SendToAllInternal(first, _data);
        };
        __sendActions[csv](csv, data);
    }

    private void SendToAllInternal(string csv, Reflective data)
    {
        if (string.IsNullOrWhiteSpace(csv)) return;
        string[] events = csv.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        if (__SimpleDebugging) Log.Write(LogLevel.Info, __SimpleTag, "Sending " + events.Length + " events: " + string.Join(", ", events) + (Group != "" ? (" to group " + Group) : ""));
        foreach (string eventName in events)
        {
            PostScriptEvent(GenerateEventName(eventName), data);
        }
    }
    #endregion

#region Variables

    enum State
    {
        Returned,
        Moving,
        Moved,
        Returning,
        Moved2
    }

    private RigidBodyComponent RigidBody;
    private Vector returnPosition;
    private Vector movedPosition;

    private Quaternion returnRotation;
    private Quaternion movedRotation;
    private Vector worldRotationAxis;
    private Vector localRotationAxis;

    private int numTurns;
    private int turnDirection;
    private int turnCount;

    private float translateSpeed;
    private float rotateSpeed;
    private State state = State.Returned;
    //private Action subscriptionClose;
    //private Action subscriptionOpen;

    const float precision = 0.04f;
    const float anglePrecision = 0.0012f;
    const float timestep = 0.25f;
    const float minimumSpeedMultipler = 0.1f;

    //SimpleData thisObjectData;

    const float PI = (float)Math.PI;
    const float TwoPI = (float)(2.0 * Math.PI);

    private string[] MoveEvent = new string[18];
    private string[] DoneEvent = new string[18];
    private Vector[] MoveVector = new Vector[18];
    private Vector[] RotateVector = new Vector[18];
    //private Vector[] PivotVector = new Vector[18];
    private float[] OverTime = new float[18];
    private Vector CurrentPivotVector;
    private Vector PositionOffset;
    private Vector RotationOffset;
    //private Vector RotationPivot;
    private string currentDoneEvent = null;
    private ScriptEventData currentData = null;

    #endregion

    public override void Init()
    {
        if (!ObjectPrivate.TryGetFirstComponent(out RigidBody))
        {
            Log.Write(LogLevel.Error, __SimpleTag, "Simple Mover requires a Rigidbody set to motion type Keyframed");
            return;
        }

        //if (RigidBody.GetMotionType() != RigidBodyMotionType.MotionTypeKeyframed)
        //{
        //    Log.Write(LogLevel.Error, __SimpleTag, "Simple Mover requires a Rigidbody set to motion type Keyframed");
        //    return;
        //}

        returnRotation = ObjectPrivate.InitialRotation;

        if (EnableEvent != "")
        {
            Log.Write("Enable Event was not null: " + EnableEvent);
            SubscribeToAll(EnableEvent, ReadInParameters);
        }
        else
        {
            ReadInParameters(null);  //executes it by passing null data
        }

        if (DisableEvent != "")
        {
            SubscribeToAll(DisableEvent, Unsubscribe);
        }
    }

    private void ReadInParameters(ScriptEventData sed)  //doesn't really pass data.  Always passes null
    {
        //Look At Animation Strings and subscribe to events
        Log.Write("In ReadInParameters");
        if (MoveTo1.Length > 0) ParseMove(0, MoveTo1);
        if (MoveTo2.Length > 0) ParseMove(1, MoveTo2);
        if (MoveTo3.Length > 0) ParseMove(2, MoveTo3);
        if (MoveTo4.Length > 0) ParseMove(3, MoveTo4);
        if (MoveTo5.Length > 0) ParseMove(4, MoveTo5);
        if (MoveTo6.Length > 0) ParseMove(5, MoveTo6);
        if (MoveTo7.Length > 0) ParseMove(6, MoveTo7);
        if (MoveTo8.Length > 0) ParseMove(7, MoveTo8);
        if (MoveTo9.Length > 0) ParseMove(8, MoveTo9);
        if (MoveTo10.Length > 0) ParseMove(9, MoveTo10);
        if (MoveTo11.Length > 0) ParseMove(10, MoveTo11);
        if (MoveTo12.Length > 0) ParseMove(11, MoveTo12);
        if (MoveTo13.Length > 0) ParseMove(12, MoveTo13);
        if (MoveTo14.Length > 0) ParseMove(13, MoveTo14);
        if (MoveTo15.Length > 0) ParseMove(14, MoveTo15);
        if (MoveTo16.Length > 0) ParseMove(15, MoveTo16);
        if (MoveTo17.Length > 0) ParseMove(16, MoveTo17);
        if (MoveTo18.Length > 0) ParseMove(17, MoveTo18);
    }

    private void Unsubscribe(ScriptEventData sed)
    {

    }

    private void ParseMove(int MoveNumber, string MoveIn)
    {
        Log.Write("In ParseMove MoveNumber: " + MoveNumber + "  M0veIn: " + MoveIn);
        List<string> MoveArray = new List<string>();
        MoveArray.Clear();
        MoveIn.Replace(" ", string.Empty);
        MoveArray = MoveIn.Split(',').ToList();
        MoveEvent[MoveNumber] = MoveArray[0];
        DoneEvent[MoveNumber] = MoveArray[1];
        MoveVector[MoveNumber].X = float.Parse(MoveArray[2]);
        MoveVector[MoveNumber].Y = float.Parse(MoveArray[3]);
        MoveVector[MoveNumber].Z = float.Parse(MoveArray[4]);
        RotateVector[MoveNumber].X = float.Parse(MoveArray[5]);
        RotateVector[MoveNumber].Y = float.Parse(MoveArray[6]);
        RotateVector[MoveNumber].Z = float.Parse(MoveArray[7]);
        //PivotVector[MoveNumber].X = float.Parse(MoveArray[8]);
        //PivotVector[MoveNumber].Y = float.Parse(MoveArray[9]);
        //PivotVector[MoveNumber].Z = float.Parse(MoveArray[10]);
        OverTime[MoveNumber] = float.Parse(MoveArray[8]);

        SubscribeToAll(MoveEvent[MoveNumber], ExecuteMovement);
        Log.Write("Move Event: " + MoveEvent[MoveNumber]);
        Log.Write("Finished ParseMove");
    }

    private void ExecuteMovement(ScriptEventData data)
    {
        Log.Write("In Execute Animation data message: " + data.Message);
        //ISimpleData idata = data.Data.AsInterface<ISimpleData>();
        //Log.Write("ObjectID: " + idata.ObjectId.ToString());
        //Log.Write("Actor: " + idata.AgentInfo.Name);
        //ObjectPrivate objectPrivate = ScenePrivate.FindObject(idata.ObjectId);
        //Log.Write("ObjectID: " + objectPrivate.ObjectId);

        if (data.Message == MoveEvent[0]) PlayMovement(0, data);
        else if (data.Message == MoveEvent[1]) PlayMovement(1, data);
        else if (data.Message == MoveEvent[2]) PlayMovement(2, data);
        else if (data.Message == MoveEvent[3]) PlayMovement(3, data);
        else if (data.Message == MoveEvent[4]) PlayMovement(4, data);
        else if (data.Message == MoveEvent[5]) PlayMovement(5, data);
        else if (data.Message == MoveEvent[6]) PlayMovement(6, data);
        else if (data.Message == MoveEvent[7]) PlayMovement(7, data);
        else if (data.Message == MoveEvent[8]) PlayMovement(8, data);
        else if (data.Message == MoveEvent[9]) PlayMovement(9, data);
        else if (data.Message == MoveEvent[10]) PlayMovement(10, data);
        else if (data.Message == MoveEvent[11]) PlayMovement(11, data);
        else if (data.Message == MoveEvent[12]) PlayMovement(12, data);
        else if (data.Message == MoveEvent[13]) PlayMovement(13, data);
        else if (data.Message == MoveEvent[14]) PlayMovement(14, data);
        else if (data.Message == MoveEvent[15]) PlayMovement(15, data);
        else if (data.Message == MoveEvent[16]) PlayMovement(16, data);
        else if (data.Message == MoveEvent[17]) PlayMovement(17, data);
    }

    private void PlayMovement(int MoveNumber, ScriptEventData data)
    {
        //Log.Write("Playing Move Number: " + MoveNumber + "  Animation: " + MoveEvent[MoveNumber]);
        //CurrentPivotVector = PivotVector[MoveNumber];
        Vector fromPosition = GetPositionOfCOM();
        Vector PositionOffset = MoveVector[MoveNumber];
        //Log.Write("PositionOffset" + PositionOffset);
        //Log.Write("(PositionOffset).Rotate(ref returnRotation): " + (PositionOffset).Rotate(ref returnRotation));
        //movedPosition = fromPosition + (PositionOffset).Rotate(ref returnRotation) + PositionOffset;
        movedPosition = fromPosition + PositionOffset;
        //Log.Write("PlayMovement fromPosition: " + fromPosition);
        //Log.Write("PlayMovement movedPosition: " + movedPosition);
        currentDoneEvent = DoneEvent[MoveNumber];
        currentData = data;
        InitializeMove(MoveNumber);

    }

    private void InitializeMove(int MoveNumber)
    {
        PositionOffset = MoveVector[MoveNumber];
        RotationOffset = RotateVector[MoveNumber];
        //RotationPivot = PivotVector[MoveNumber];
        float MoveDuration = OverTime[MoveNumber];

        //Rotation Pivot
        //RigidBody.SetCenterOfMass(RotationPivot);

        Quaternion rotation = Quaternion.FromEulerAngles(Mathf.RadiansPerDegree * RotationOffset);
        //returnRotation = ObjectPrivate.InitialRotation;
        movedRotation = returnRotation * rotation;

        numTurns = (int)((RotationOffset.Length() + 179.0f) / 360f);

        bool noRotation = RotationOffset.Length() < 0.5f;

        if (noRotation)
        {
            worldRotationAxis = Vector.Up;
        }
        else
        {
            float rotationAngle;
            GetAngleAxis(rotation, out rotationAngle, out localRotationAxis);

            if (Math.Abs(rotationAngle % TwoPI) < 0.001f)
            {
                // rotation axis won't be calculated correctly for exact multiple of 360 rotation
                // adjust euler angles slightly and re-calculate
                float x = RotationOffset.X;
                float y = RotationOffset.Y;
                float z = RotationOffset.Z;
                if (x != 0) x = Math.Sign(x) * (Math.Abs(x) - 1.0f);
                if (y != 0) y = Math.Sign(y) * (Math.Abs(y) - 1.0f);
                if (z != 0) z = Math.Sign(z) * (Math.Abs(z) - 1.0f);
                Vector adjustedOffset = new Vector(x, y, z);
                Quaternion adjustedRotation = Quaternion.FromEulerAngles(Mathf.RadiansPerDegree * adjustedOffset);
                float tempAngle;
                GetAngleAxis(adjustedRotation, out tempAngle, out localRotationAxis);
            }
            worldRotationAxis = localRotationAxis.Rotate(ref returnRotation);
        }

        float moveAngle = GetAngleFromZero(movedRotation);
        //rotateSpeed = Math.Abs(moveAngle + numTurns * TwoPI) / (MoveDuration * SpeedCurveAverage());
        rotateSpeed = Math.Abs(moveAngle + numTurns * TwoPI) / (MoveDuration);

        //Log.Write("movedRotation: " + movedRotation + " moveAngle: " + moveAngle + " numTurns: " + numTurns + " MoveDuration: " + MoveDuration + " SpeedCirveAverage: " + SpeedCurveAverage() + " rotateSpeed: " + rotateSpeed);
             
        if (!noRotation)
        {
            Quaternion unitRotation = FromAngleAxis(1f, localRotationAxis);
            turnDirection = Math.Sign(GetAngleFromZero(returnRotation * unitRotation));
        }

        translateSpeed = (movedPosition - returnPosition).Length() / (MoveDuration * SpeedCurveAverage());

        if (__SimpleDebugging)
        {
            Log.Write("rotation angle:" + moveAngle + " around:" + localRotationAxis + " world space axis:" + worldRotationAxis + " revolutions:" + numTurns);
        }

        //Log.Write("InitializeMove state: " + state);

        StartCoroutine(Move);

    }

    void Move()
    {
        //Log.Write("In Move");
        state = State.Moving;
        Vector fromPosition = GetPositionOfCOM();

        Quaternion fromRotation = RigidBody.GetOrientation();

        float fromAngle = GetAngleFromZero(fromRotation) + turnDirection * turnCount * TwoPI;
        float toAngle = GetAngleFromZero(movedRotation) + turnDirection * numTurns * TwoPI;

        bool translateDone = false;
        bool rotateDone = false;
        bool rotateWillComplete = false;

        if (__SimpleDebugging)
        {
            Log.Write("Open, " + Mathf.DegreesPerRadian * fromAngle + " -> " + Mathf.DegreesPerRadian * toAngle + " axis " + worldRotationAxis + " speed " + rotateSpeed);
        }
        //Log.Write("FromRotation: " + fromRotation + " MovedRotation: " + movedRotation);
        //Log.Write("fromAngle: " + fromAngle + " toAngle: " + toAngle + " movedRotation: " + movedRotation + " rotateSpeed: " + rotateSpeed + " translateSpeed: " + translateSpeed);

        while (state == State.Moving)
        {
            if (PositionOffset.X == 0)
                if (PositionOffset.Y == 0)
                    if (PositionOffset.Z == 0)
                    {
                        Log.Write("Position Offset is <0,0,0>");
                        translateDone = true;
                    }

            if (!translateDone) ApplyTranslation(fromPosition, movedPosition, translateSpeed, ref translateDone);

            if (RotationOffset.X == 0)
                if (RotationOffset.Y == 0)
                    if (RotationOffset.Z == 0)
                    {
                        Log.Write("Rotation Offset is <0,0,0>");
                        rotateDone = true;
                    }

            if (!rotateDone) ApplyRotation(fromAngle, toAngle, movedRotation, rotateSpeed, ref rotateDone, ref rotateWillComplete);

            Log.Write("TranslateDone: " + translateDone + " RotateDone: " + rotateDone);

            if (translateDone && (rotateDone || rotateWillComplete))
            {
                RigidBody.SetOrientation(movedRotation, (e) =>
                {
                    //SetPositionOfCOM(movedPosition);
                });

                returnRotation = movedRotation;
                Log.Write("In Move Setting state to Moved");
                state = State.Moved;
                Wait(TimeSpan.FromSeconds(timestep));
                SendToAll(currentDoneEvent, currentData.Data);
                Log.Write("Sent Done Event: " + currentDoneEvent);
                break;
            }
            Wait(TimeSpan.FromSeconds(timestep));
        }
        //Log.Write("Finished Move");
    }

    void ApplyTranslation(Vector startPosition, Vector targetPosition, float speed, ref bool isComplete)
    {
        //Log.Write("In ApplyTranslation");
        Vector totalOffset = targetPosition - startPosition;

        if (totalOffset.Length() <= precision)
        {
            isComplete = true;
            return;
        }
        Vector moveDirection = totalOffset.Normalized();

        Vector currentOffset = targetPosition - GetPositionOfCOM();
        //Log.Write("ApplyTransform currentOffset: " + currentOffset + " targetPosition: " + targetPosition + " GetPositionOfCOM(): " + GetPositionOfCOM());

        if (currentOffset.Length() < precision)
        {
            RigidBody.SetLinearVelocity(Vector.Zero);
            isComplete = true;
            return;
        }

        float distanceToTarget = moveDirection.Dot(ref currentOffset);

        if (distanceToTarget < 0) // overshot
        {
            RigidBody.SetLinearVelocity(Vector.Zero);
            isComplete = true;
            return;
        }

        if (distanceToTarget < speed * timestep) // slow down if we think we will overshoot next timestep 
        {
            speed = distanceToTarget / timestep;
        }

        Vector velocity = speed * moveDirection;
        //Log.Write("timestep: " + timestep + " DistanceToTarget: " + distanceToTarget + " velocity: " + velocity + " speed: " + speed + " moveDirection: " + moveDirection);
        RigidBody.SetLinearVelocity(velocity);
    }

    void ApplyRotation(float startAngle, float targetAngle, Quaternion targetRotation, float speed, ref bool isComplete, ref bool willComplete)
    {
        float totalAngle = targetAngle - startAngle;

        if (Math.Abs(totalAngle) < anglePrecision)
        {
            Log.Write("1");
            isComplete = true;
            return;
        }

        int sign = Math.Sign(totalAngle);

        Quaternion currentRotation = RigidBody.GetOrientation();

        float angleNoTurn = GetAngleFromZero(currentRotation);
        float angle = angleNoTurn + turnDirection * turnCount * TwoPI;

        float angleOffset = sign * (targetAngle - angle);
        //Log.Write("totalAngle" + totalAngle + " angleNoTurn: " + angleNoTurn + " angle: " + angle + " angleOffset: " + angleOffset);
        if (willComplete || Math.Abs(angleOffset) < anglePrecision)
        {
            RigidBody.SetAngularVelocity(Vector.Zero);
            RigidBody.SetOrientation(targetRotation);
            Log.Write("2");
            isComplete = true;
            return;
        }

        if (angleOffset < 0) // overshot
        {
            RigidBody.SetAngularVelocity(Vector.Zero);
            RigidBody.SetOrientation(targetRotation);
            Log.Write("3");
            isComplete = true;
            return;
        }

        if (angleOffset < speed * timestep)
        {
            speed = angleOffset / timestep;
            willComplete = true;
        }

        Vector velocity = sign * speed * worldRotationAxis;
        RigidBody.SetAngularVelocity(velocity);

        if (willComplete)
        {
            Log.Write("4");
            return;
        }

        float prediction = Math.Abs(angleNoTurn + sign * timestep * speed);
        if (prediction >= PI || Math.Abs(angleNoTurn) > PI && prediction < PI)
        {
            turnCount += sign;
        }
    }

    float SpeedCurve(float p, float totalP)
    {
        //       1 |     ______    
        //         |   /        \
        //         |  /          \
        // minimum |_              _
        //         |
        //       0 |________________ totalP
        //         ^---^       ^----^ 
        //       rampUpLength  rampDownLength
        if (p < 0f) p = 0f;
        if (p > totalP) p = totalP;

        //float rampUpEnd = EaseInOut * totalP / 2f;
        //float rampDownStart = totalP * (1f - EaseInOut / 2f);

        float rampUpEnd = totalP / 2f;
        float rampDownStart = totalP * (1f/2f);
        if (p < rampUpEnd)
        {
            float t = p / rampUpEnd;
            t = t * t * (3.0f - 2.0f * t); // apply ease in/out
            t = minimumSpeedMultipler + t * (1f - minimumSpeedMultipler);
            return t;
        }
        else if (p > rampDownStart)
        {
            float t = 1f - (p - rampDownStart) / (totalP - rampDownStart); // 1 at start of rampdown, 0 at end (p = duration)
            t = t * t * (3.0f - 2.0f * t); // apply ease in/out
            t = minimumSpeedMultipler + t * (1f - minimumSpeedMultipler);
            return t;
        }

        return 1f;
    }

    float SpeedCurveAverage()
    {
        float rampAverage = (1f - minimumSpeedMultipler) / 2f;
        //return EaseInOut * rampAverage + (1f - EaseInOut);
        return rampAverage + (1f);
    }

    float GetAngleFromZero(Quaternion q)
    {
        Quaternion relative = InverseQ(returnRotation) * q;

        float angle;
        Vector axis;

        GetAngleAxis(relative, out angle, out axis);
        if (axis.Dot(ref localRotationAxis) < 0)
        {
            angle = -angle;
        }

        if (angle < -PI)
        {
            angle += TwoPI;
        }
        else if (angle > PI)
        {
            angle -= TwoPI;
        }
        return angle;
    }

    void SetPositionOfCOM(Vector v)
    {
        Vector localCOM = RigidBody.GetCenterOfMass();
        Quaternion rot = RigidBody.GetOrientation();
        Vector offset = localCOM.Rotate(ref rot);
        Log.Write("v: " + v);
        Log.Write("offset: " + offset);
        RigidBody.SetPosition(v - offset);
    }

    Vector GetPositionOfCOM()
    {
        Quaternion rot = RigidBody.GetOrientation();
        return RigidBody.GetPosition() + CurrentPivotVector.Rotate(ref rot);
    }

    static Quaternion InverseQ(Quaternion q)
    {
        return new Quaternion(-q.X, -q.Y, -q.Z, q.W);
    }

    static void GetAngleAxis(Quaternion q, out float angle, out Vector axis)
    {
        axis = new Vector(0, 0, 1);
        if (q.W > 1.0f || q.W < -1.0f) q = q.Normalized();
        angle = Math.Abs(2f * (float)Math.Acos(q.W));
        float s = (float)Math.Sqrt(1.0 - q.W * q.W);
        if (s > 0.001f)
        {
            axis.X = q.X / s;
            axis.Y = q.Y / s;
            axis.Z = q.Z / s;
        }
    }

    static Quaternion FromAngleAxis(float angle, Vector axis)
    {
        float s = (float)Math.Sin(angle / 2f);
        float x = axis.X * s;
        float y = axis.Y * s;
        float z = axis.Z * s;
        float w = (float)Math.Cos(angle / 2f);
        return new Quaternion(x, y, z, w);
    }

}


