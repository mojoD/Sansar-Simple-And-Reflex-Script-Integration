/* 
 * This work uses content from the Sansar Knowledge Base. © 2017 Linden Research, Inc. Licensed under the Creative Commons Attribution 4.0 International License (license summary available at https://creativecommons.org/licenses/by/4.0/ and complete license terms available at https://creativecommons.org/licenses/by/4.0/legalcode).
 * This work uses content from LGG. © 2017 Greg Hendrickson. Licensed under the Creative Commons Attribution 4.0 International License (license summary available at https://creativecommons.org/licenses/by/4.0/ and complete license terms available at https://creativecommons.org/licenses/by/4.0/legalcode).
 */

using Sansar.Script;
using Sansar.Simulation;
using System;
using System.Collections.Generic;

public class ProximityEvent : SceneObjectScript
{
    private class NearbyVisitor
    {
        public DateTime FirstDetectedTime;
        public DateTime LastDetectedTime;
        public string name;
        public AgentPrivate agentOjb;
        public ObjectPrivate objectObj;
        public AnimationComponent animObj;
        public TimeSpan TimeSenseLastDetection
        {
            get { return DateTime.Now - LastDetectedTime; }
        }
    }
    //How far away from the center to look for people
    public float Detection_Radius = 4.0f;
    //How long to wait before we fire the detect event again, if they are STILL nearby
    public float Secs_Before_ReDetect = 10.0f;
    //The center to detect out from 
    public Sansar.Vector Custom_Detect_Loc;
    //How often to scan for new people
    public float Scans_Per_Minute = 10;
    //If we detect someone, pause new detections for this long (could be used to let a long sound play)
    public float Pause_After_Detect_Secs = 0;
    //Trigger this sound if.. filled out
    public SoundResource Sound_To_Play;
    //If true, we use their client to play the sound locally
    public Boolean Play_Sound_To_Everyone = false;
    //Show this text to the colliding avatar
    public string Text_To_Show;
    //where to TP the avatar to when detected
    public Sansar.Vector TP_To_Location;
    //Note: only one parameter left on this script, maybe save for script channel


    public Boolean Debug_To_Log = false;

    //Private vars
    private RigidBodyComponent RigidBody;
    private AudioComponent LocalAudioComponent;

    private Dictionary<string, NearbyVisitor> NearbyVisitors = new Dictionary<string, NearbyVisitor>();

    public override void Init()
    {
        if (RigidBody == null)
        {
            if (!ObjectPrivate.TryGetFirstComponent(out RigidBody))
            {
                // This example will only work on a dynamic object with a rigid body. Bail if there isn't one.
                return;
            }
        }
        if (LocalAudioComponent == null)
        {
            if (!ObjectPrivate.TryGetFirstComponent(out LocalAudioComponent))
            {
                //return;
            }
        }
        Custom_Detect_Loc.W = TP_To_Location.W = 0;

        if (Custom_Detect_Loc.LengthSquared() < .01) Custom_Detect_Loc = RigidBody.GetPosition();
        StartCoroutine(scanForAvatarsCoroutine);
    }
    private void scanForAvatarsCoroutine()
    {
        while (true)
        {
            foreach (AgentPrivate agent in ScenePrivate.GetAgents())
            {
                ObjectPrivate agentObejct = ScenePrivate.FindObject(agent.AgentInfo.ObjectId);
                //So there was a bug where name was returning blank for everyone, thus breaking the script.  This sacrifices some performance in exchange to hopefully stop LL from breaking it
                string key = string.Format("{0}|{1}|{2}|{3}", agent.AgentInfo.Name, agent.AgentInfo.SessionId.ToString(), agent.AgentInfo.ObjectId, agent.AgentInfo.AvatarUuid);
                AnimationComponent anim;
                if (agentObejct.TryGetFirstComponent(out anim))
                {
                    //This is a faster way to check distance,thanks Dolphin
                    if ((agentObejct.Position - Custom_Detect_Loc).LengthSquared() < (Detection_Radius * Detection_Radius))
                    {
                        //agent is close enough, see if we have already detected him recently
                        NearbyVisitor visitor;
                        if (NearbyVisitors.TryGetValue(key, out visitor))
                        {
                            //already detected before
                            if (visitor.TimeSenseLastDetection > TimeSpan.FromSeconds(Secs_Before_ReDetect))
                            {
                                //he hasn't been detected recently, do the event again
                                visitor.LastDetectedTime = DateTime.Now;
                                VisitorProximityDetected(visitor);
                            }
                        }
                        else
                        {
                            //not detected before
                            visitor = new NearbyVisitor();
                            visitor.name = agent.AgentInfo.Name;
                            visitor.objectObj = agentObejct;
                            visitor.agentOjb = agent;
                            visitor.animObj = anim;
                            visitor.LastDetectedTime = visitor.FirstDetectedTime = DateTime.Now;
                            NearbyVisitors[key] = visitor;
                            //New agent, fire the event
                            VisitorProximityDetected(visitor);
                        }
                    }
                }
            }

            Wait(TimeSpan.FromSeconds(60.0 / Scans_Per_Minute));
        }
    }
    private void VisitorProximityDetected(NearbyVisitor visitor)
    {
        if (Debug_To_Log)
            Log.Write(string.Format("Visitor Proximity detected!  We were scanning for agents withing {0} meters of {1}, we found {2} at {3} , and they were first detected at {4}, their position is {5} and their camera is facing in direction {6}",
            Detection_Radius,
            Custom_Detect_Loc,
            visitor.agentOjb.AgentInfo.Name,
            visitor.LastDetectedTime,
            visitor.FirstDetectedTime,
            visitor.objectObj.Position,
            visitor.animObj.GetVectorAnimationVariable("LLCameraForward")
            ));

        if (Sound_To_Play != null)
        {
            playASound(visitor);
        }

        if (Text_To_Show.Length > 0)
        {
            visitor.agentOjb.Client.SendChat(Text_To_Show);
        }

        if (TP_To_Location.LengthSquared() < .01)
        {
            visitor.animObj.SetPosition(TP_To_Location);
        }

        Wait(TimeSpan.FromSeconds(Pause_After_Detect_Secs));
    }
    private void playASound(NearbyVisitor visitor)
    {
        if (Play_Sound_To_Everyone)
        {
            if (LocalAudioComponent != null)
            {
                LocalAudioComponent.PlaySoundOnComponent(Sound_To_Play, PlaySettings.PlayOnce);
            }
            else
            {
                ScenePrivate.PlaySoundAtPosition(Sound_To_Play, visitor.objectObj.Position, PlaySettings.PlayOnce);
            }
        }
        else
        {
            visitor.agentOjb.PlaySound(Sound_To_Play, PlaySettings.PlayOnce);
        }
    }
}