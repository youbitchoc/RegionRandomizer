using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using static RegionRandomizer.LogicalRando;

namespace RegionRandomizer
{
    internal class LogicalRando
    {
        public static List<Connectible> RandomlyConnectConnectibles(List<Connectible> connectibles, float randomness = 0.5f, int triesRemaining = 5)
        {
            try
            {
                //make list of connectibles that need to be connected
                List<Connectible> notConnected = new();
                List<Connectible> connected = new();

                //always add the first Connectible in the input list to connected
                //if I want it to be random, I should shuffle before calling the function
                connected.Add(connectibles[0].FreshClone());

                for (int i = 1; i < connectibles.Count; i++)
                    notConnected.Add(connectibles[i].FreshClone());


                /*
                 * STRATEGY #1: the dumb strategy:
                 *  Pick a random unconnected connection from "connected"
                 *  Determine the lowest connection cost of the connectibles in "connected"
                 *  Determine the lowest connection cost from snapping a connectible from "notConnected"
                 *  If the "notConnected" cost is less than half the "connected" cost OR there is only one unconnected connection,
                 *      snap the "notConnected" to the connection and add it to "connected"
                 *      otherwise, make the connection between the two "connected" connectibles
                 *  Repeat until there are no "notConnected"s left
                 *  Then... either forcibly connect the remaining connections... or don't...?
                 *  
                 *  (A significant refinement would be to prioritize "notConnected"s early on,
                 *  but prioritize "connected"s later on)
                */

                //connected.Shuffle(); //randomized easy as that. My pick is now "connected[0]"
                //no; doesn't work: connected[0] might have no available connections


                //connect all of notConnected to connected
                string connectionPattern = "Connection Pattern: ";
                while (notConnected.Count > 0)
                {
                    //cycle through each of notConnected, get its best possible score and where
                    //then pick the best scored option. man, this is about to be a lot of recursion...
                    float lowestScore = float.PositiveInfinity;
                    int lowestNotConnectedIdx = -1;
                    string lowestNotConnectedConn = "";
                    int lowestConnectedIdx = -1;
                    string lowestConnectedConn = "";

                    int freeConnectionCount = 0;
                    foreach (Connectible c in connected)
                    {
                        foreach (string conn in c.connections.Values)
                        {
                            if (conn == "")
                                freeConnectionCount++;
                        }
                    }

                    for (int notIdx = 0; notIdx < notConnected.Count; notIdx++) //cycle through notConnected
                    {
                        Connectible nc = notConnected[notIdx];

                        if (notConnected.Count > 1 && nc.connections.Count < 2 && freeConnectionCount < 2)
                            continue; //prevents connecting a dead-end when there is only one free connection left

                        for (int connectedIdx = 0; connectedIdx < connected.Count; connectedIdx++) //cycle through connected
                        {
                            Connectible cc = connected[connectedIdx];
                            foreach (string conn in cc.connections.Keys) //cycle through each free connection in connected
                            {
                                if (cc.connections[conn] != "") //only process free connections
                                    continue;
                                var connData = cc.ResolveBestConnection(nc, conn, connected);
                                float score = connData.Value;

                                score = score - score * UnityEngine.Random.value * randomness; //randomize score

                                if (score < lowestScore)
                                {
                                    lowestNotConnectedIdx = notIdx;
                                    lowestNotConnectedConn = connData.Key;
                                    lowestConnectedIdx = connectedIdx;
                                    lowestConnectedConn = conn;
                                    lowestScore = score;
                                }
                            }
                        }
                    }

                    if (lowestNotConnectedIdx < 0 || lowestConnectedIdx < 0)
                        RegionRandomizer.LogSomething("Could not find any notConnected to connect to connected!! notConnected.Count: " + notConnected.Count);

                    //connect the best option!
                    Connectible best = notConnected[lowestNotConnectedIdx];

                    connected.Add(best); //added here so that all the proper potential connections are ensured to be removed

                    best.ConnectToOtherConnectible(connected[lowestConnectedIdx], lowestNotConnectedConn, lowestConnectedConn, connected);

                    //connected.Add(best);
                    notConnected.RemoveAt(lowestNotConnectedIdx);

                    connectionPattern += best.name + " to " + connected[lowestConnectedIdx].name + " (" + lowestScore + "); ";
                }
                RegionRandomizer.LogSomething(connectionPattern);
                notConnected.Clear(); //we have no use of this anymore


                //now that everything is in connected, start making connections!
                connectionPattern = "Connection Pattern 2: ";
                bool thingsStillUnconnected = true;
                while (thingsStillUnconnected)
                {
                    //pick the connectible with the highest number of free connections
                    int connIdx = -1;
                    int highestFreeCount = 0;
                    List<string> freeConns = new();
                    for (int i = 0; i < connected.Count; i++)
                    {
                        List<string> free = new();
                        foreach (var conn in connected[i].connections)
                        {
                            if (conn.Value == "")
                            {
                                if (connected[i].potentialConnectionScores[conn.Key].Count > 0)
                                    free.Add(conn.Key);
                                else {
                                    //RegionRandomizer.LogSomething(connectionPattern + " ... FAILED!");
                                    RegionRandomizer.LogSomething("No potential connections for " + connected[i].name + ": " + conn.Key);
                                }
                            }
                        }
                        if (free.Count > highestFreeCount)
                        {
                            highestFreeCount = free.Count;
                            freeConns.Clear();
                            freeConns = free;
                            connIdx = i;
                        }
                        else
                            free.Clear();
                    }

                    if (highestFreeCount < 1)
                    {
                        thingsStillUnconnected = false;
                        break;
                    }

                    //pick a random one of freeConnections
                    //int freeConnRandIdx = UnityEngine.Random.Range(0, freeConns.Count);
                    //string connName = freeConns[freeConnRandIdx];

                    //pick the free connection with the highest (worst) first potential connection score
                    int highestPotentialScoreIdx = 0;
                    float highestPotentialScore = float.NegativeInfinity;
                    for (int i = 0; i < freeConns.Count; i++)
                    {
                        if (connected[connIdx].potentialConnectionScores[freeConns[i]][0] > highestPotentialScore)
                        {
                            highestPotentialScore = connected[connIdx].potentialConnectionScores[freeConns[i]][0];
                            highestPotentialScoreIdx = i;
                        }
                    }
                    string connName = freeConns[highestPotentialScoreIdx];

                    float lowestScore = float.PositiveInfinity;
                    int lowestIdx = -1;
                    string lowestConn = "";
                    int testsSinceLowerScore = int.MinValue;

                    for (int i = 0; i < connected[connIdx].potentialConnections[connName].Count; i++)
                    {
                        float score = connected[connIdx].potentialConnectionScores[connName][i];
                        score += connected[connIdx].ScoreDiffWhenConnectionTaken(connected[connIdx].name + ";" + connName, connected[connIdx].potentialConnections[connName][i], connected);

                        score = score - score * UnityEngine.Random.value * randomness; //randomize score

                        if (score < lowestScore)
                        {
                            lowestScore = score;
                            lowestConn = connected[connIdx].potentialConnections[connName][i].Split(';')[1];
                            string cname = connected[connIdx].potentialConnections[connName][i].Split(';')[0];
                            for (int j = 0; j < connected.Count; j++)
                            {
                                if (connected[j].name == cname)
                                {
                                    lowestIdx = j;
                                    break;
                                }
                            }
                            testsSinceLowerScore = 0;
                        }
                        else
                        {
                            testsSinceLowerScore++;
                            if (testsSinceLowerScore > 2)
                                break;
                        }
                    }

                    if (lowestIdx < 0)
                    {
                        RegionRandomizer.LogSomething("Couldn't find any other free connection!! " + connected[connIdx].name + ", " + connName);
                        //continue;
                        break;
                    }

                    connected[connIdx].MakeConnection(connected[lowestIdx], connName, lowestConn, connected);

                    connectionPattern += connected[connIdx].name + ";" + connName + "--" + connected[lowestIdx].name + ";" + lowestConn + " (" + lowestScore + "); ";
                    //if (!freeConnections.Remove(new KeyValuePair<int, string>(lowestIdx, lowestConn)))
                        //RegionRandomizer.LogSomething("Failed to remove connection " + lowestConn);

                }

                RegionRandomizer.LogSomething(connectionPattern);


                //check that everything was done properly
                if (triesRemaining > 0)
                {
                    foreach (Connectible c in connected)
                    {
                        if (c.connections.Values.Contains("") || c.connections.Values.Contains(c.name))
                        {
                            RegionRandomizer.LogSomething("Missing a connection!! Rerandomizing connectibles.");

                            connectibles.Shuffle(); //try starting with a different connectible; just in case
                            connected.Clear();

                            return RandomlyConnectConnectibles(connectibles, (randomness < 1f) ? randomness + 0.1f : randomness, --triesRemaining);
                        }
                    }
                }

                return connected;

                /*
                List<KeyValuePair<int, string>> freeConnections = new();

                //add free connections from connected[0] (which has not yet been touched)
                foreach (string conn in connected[0].connections.Keys)
                    freeConnections.Add(new KeyValuePair<int, string>(0, conn));

                string debugText = "Connection pattern: ";
                int errorCounter = 0;

                //loop starts here?
                while (freeConnections.Count > 0)
                {
                    //if freeConnections.Count < 1 break;

                    //pick random free connection
                    //freeConnections.Shuffle();
                    //var freeConnectionInfo = freeConnections.Pop();
                    int freeConnRandIdx = UnityEngine.Random.Range(0, freeConnections.Count);
                    var freeConnectionInfo = freeConnections[freeConnRandIdx];
                    freeConnections.RemoveAt(freeConnRandIdx);
                    int connIdx = freeConnectionInfo.Key;
                    string connName = freeConnectionInfo.Value;

                    int notConnectedConnCount = 0;
                    foreach (Connectible c in notConnected)
                        notConnectedConnCount += c.connections.Count;

                    //find cheapest in connected
                    float lowestConnectedScore = float.PositiveInfinity;
                    int lowestConnectedIdx = -1;
                    string lowestConnectedConnection = "";
                    for (int i = 0; i < connected.Count; i++)
                    {
                        if (i == connIdx)
                            continue;
                        var connectionScore = connected[connIdx].LowestConnectionScore(connected[i], connName);
                        //float score = connected[i].TotalDistanceScore(connected) + connectionScore.Value;
                        float score = connectionScore.Value;
                        //randomize score
                        score = score - score * UnityEngine.Random.value * randomness;

                        if (score < lowestConnectedScore)
                        {
                            lowestConnectedScore = score;
                            lowestConnectedConnection = connectionScore.Key;
                            lowestConnectedIdx = i;
                        }
                    }

                    //find cheapest in notConnected... through... brute force...?
                    //sure... what could go wrong with inefficiency!?
                    float lowestNotConnectedScore = float.PositiveInfinity;
                    int lowestNotConnectedIdx = -1;
                    string lowestNotConnectedConnection = "";
                    for (int i = 0; i < notConnected.Count; i++)
                    {
                        //check that I'm not connecting a one connection region (e.g: LC) when there's only 1 available connection
                        if (notConnected[i].connections.Count < 2 && freeConnections.Count <= 0 && notConnectedConnCount > 1)
                            continue;

                        //var connectionScore = connected[connIdx].LowestConnectionScore(notConnected[i], connName, true, connected);
                        var connectionScore = connected[connIdx].ResolveBestConnection(notConnected[i], connName, connected);
                        //notConnected[i].SnapToConnection(connected[connIdx], connectionScore.Key, connName);
                        float score = connectionScore.Value;// + notConnected[i].TotalDistanceScore(connected);
                        //randomize score
                        score = score - score * UnityEngine.Random.value * randomness;

                        if (score < lowestNotConnectedScore)
                        {
                            lowestNotConnectedScore = score;
                            lowestNotConnectedConnection = connectionScore.Key;
                            lowestNotConnectedIdx = i;
                        }
                    }

                    //determine connected vs. notConnected points scaling

                    float connectedPreference = 1024f * Square((float)freeConnections.Count / (float)notConnectedConnCount);
                    //I'm really not sure about this arbitrary modifier...


                    //logic checks to ensure no incorrectable situations occur
                    bool canDoNotConnected = lowestNotConnectedIdx >= 0;
                    //bool canDoConnected = lowestConnectedIdx >= 0 && (freeConnections.Count - 1 > freeConnectionsNeeded + 4 || !canDoNotConnected);

                    //figure out how many free connections are needed to fit in all of notConnected
                    bool enoughFreeConnectionsToAvoidError = true;
                    if (lowestConnectedIdx >= 0 && canDoNotConnected)
                    {
                        List<int> tempFreeConnections = new();
                        foreach (var conn in freeConnections)
                            tempFreeConnections.Add(-conn.Key);
                        tempFreeConnections.Remove(-lowestConnectedIdx);
                        try
                        {
                            tempFreeConnections.RemoveAt(UnityEngine.Random.Range(0, tempFreeConnections.Count));
                            tempFreeConnections.RemoveAt(UnityEngine.Random.Range(0, tempFreeConnections.Count));
                            tempFreeConnections.RemoveAt(UnityEngine.Random.Range(0, tempFreeConnections.Count));
                            tempFreeConnections.RemoveAt(UnityEngine.Random.Range(0, tempFreeConnections.Count));
                            tempFreeConnections.RemoveAt(UnityEngine.Random.Range(0, tempFreeConnections.Count));
                            tempFreeConnections.RemoveAt(UnityEngine.Random.Range(0, tempFreeConnections.Count));
                        }
                        catch (Exception ex) { }

                        List<int> notConnectedConnCounts = new();
                        List<List<int>> hypotheticalConnections = new();
                        for (int i = 0; i < notConnected.Count; i++)
                        {
                            notConnectedConnCounts.Add(notConnected[i].connections.Count);
                            hypotheticalConnections.Add(new List<int>());
                        }
                        notConnectedConnCounts[lowestNotConnectedIdx] = notConnectedConnCounts[lowestNotConnectedIdx] - 1; //because at least 1 must be connected
                                                                                                                           //int freeConnectionsNeeded = 0;
                        for (int i = 0; i < notConnected.Count; i++)
                        {
                            int conns = notConnected[i].connections.Count;
                            for (int j = 0; j < notConnected.Count && conns > 0; j++)
                            {
                                if (i != j && !hypotheticalConnections[i].Contains(j) && notConnectedConnCounts[j] > 0)
                                {
                                    conns--;
                                    hypotheticalConnections[j].Add(i);
                                    notConnectedConnCounts[j] = notConnectedConnCounts[j] - 1;
                                }
                            }
                            //freeConnectionsNeeded += conns;
                            for (int f = tempFreeConnections.Count - 1; f >= 0 && conns > 0; f--)
                            {
                                if (!hypotheticalConnections[i].Contains(tempFreeConnections[f]))
                                {
                                    conns--;
                                    hypotheticalConnections[i].Add(tempFreeConnections[f]);
                                    tempFreeConnections.RemoveAt(f);
                                }
                            }
                            if (conns > 0)
                            {
                                enoughFreeConnectionsToAvoidError = false;
                                break;
                            }
                        }
                        notConnectedConnCounts.Clear();
                        hypotheticalConnections.Clear();
                        tempFreeConnections.Clear();
                    }

                    bool canDoConnected = lowestConnectedIdx >= 0 && ((enoughFreeConnectionsToAvoidError && freeConnections.Count > 1) || !canDoNotConnected);

                    //connect notConnected, if its score is lower
                    if (canDoNotConnected && (!canDoConnected || lowestNotConnectedScore * connectedPreference < lowestConnectedScore))
                    {
                        Connectible c = notConnected[lowestNotConnectedIdx];
                        notConnected.RemoveAt(lowestNotConnectedIdx);

                        c.SnapToConnection(connected[connIdx], lowestNotConnectedConnection, connName);
                        c.MakeConnection(connected[connIdx], lowestNotConnectedConnection, connName);

                        connected.Add(c);

                        //add free conns
                        foreach (string conn in c.connections.Keys)
                        {
                            if (conn == lowestNotConnectedConnection) //don't add the connection I just made
                                continue;
                            freeConnections.Add(new KeyValuePair<int, string>(connected.Count - 1, conn));
                        }

                        debugText += "not(" + lowestConnectedScore + "," + (lowestNotConnectedScore * connectedPreference) + ")," + c.name + "," + connected[connIdx].name + "; ";
                    }
                    //connect connected, if it's valid
                    else if (canDoConnected)
                    {
                        connected[lowestConnectedIdx].MakeConnection(connected[connIdx], lowestConnectedConnection, connName);

                        //remove the conn I just made
                        //KeyValuePair<int, string> pairToRemove = new(lowestConnectedIdx, lowestConnectedConnection);
                        //freeConnections.Remove(pairToRemove);
                        int oldLength = freeConnections.Count; //debug
                        for (int i = 0; i < freeConnections.Count; i++)
                        {
                            if (freeConnections[i].Key == lowestConnectedIdx && freeConnections[i].Value == lowestConnectedConnection)
                            {
                                freeConnections.RemoveAt(i);
                                break;
                            }
                        }
                        if (freeConnections.Count == oldLength) //debug
                            RegionRandomizer.LogSomething("Failed to remove connection " + connected[lowestConnectedIdx].name + " " + lowestConnectedConnection);

                        debugText += "conn(" + lowestConnectedScore + "," + (lowestNotConnectedScore * connectedPreference) + ")," + connected[lowestConnectedIdx].name + "," + connected[connIdx].name + "; ";
                    }
                    else
                    {
                        RegionRandomizer.LogSomething("Failed to connect everything! " + freeConnections.Count + " " + connName + " " + connected.Count + " " + notConnected.Count + " " + lowestConnectedIdx + " " + lowestNotConnectedIdx);

                        //if free connections still left
                        if (freeConnections.Count <= errorCounter)
                        {
                            RegionRandomizer.LogSomething(debugText);
                            if (randomness < 0.3f)
                            {
                                //debug output
                                string debugText2 = "";
                                foreach (Connectible conn in connected)
                                    debugText2 += conn.name + ": " + conn.position.ToString() + "; ";
                                for (int i = 0; i < connected.Count; i++)
                                {
                                    debugText2 += " . . . " + connected[i].name + ": ";
                                    foreach (var conn in connected[i].connections)
                                        debugText2 += conn.Key + ":" + conn.Value + ", ";
                                }
                                RegionRandomizer.LogSomething(debugText2);
                            }

                            connectibles.Shuffle(); //try starting with a different connectible; just in case
                            connected.Clear();
                            notConnected.Clear();
                            freeConnections.Clear();

                            return RandomlyConnectConnectibles(connectibles, (randomness < 1f) ? randomness + 0.1f : randomness);
                        }

                        errorCounter++;
                        freeConnections.Add(freeConnectionInfo); //add the connection back to the stack
                    }

                    //I guess nothing else is needed...?
                }

                RegionRandomizer.LogSomething(debugText);

                if (notConnected.Count > 0)
                {
                    RegionRandomizer.LogSomething("Failed to connect some of notConnected: " + notConnected.Count);

                    connectibles.Shuffle(); //try starting with a different connectible; just in case
                    connected.Clear();
                    notConnected.Clear();
                    freeConnections.Clear();

                    return RandomlyConnectConnectibles(connectibles, (randomness < 1f) ? randomness + 0.1f : randomness);
                }

                return connected;
                */
            }
            catch (Exception ex)
            {
                RegionRandomizer.LogSomething(ex);
                return new List<Connectible>();
            }
        }

        //connectible class
        #region Connectible
        public class Connectible
        {
            //ARBITRARY NUMBERS!!!
            //private const float EST_DIST_SQR = 1000f * 1000f; //the estimated distance between two rooms, squared
            public const bool PROHIBIT_DOUBLE_CONNECTIONS = true; //prevents connection[a] and connection[b] from having the same value
            public const float DISTANCE_SCORE_MODIFIER = 150f;
            public const float CONNECTION_DISTANCE_MODIFIER = 1f;
            public const float PLACEMENT_ANGLE_MODIFIER = 1f;
            public const float ANGLE_SCORE_MODIFIER = 1f; //set to lower because it's not important in vanilla (e.g: LF and SB)
            public const float BONUS_ANGLE_PLACEMENT_MODIFIER = 5f; //applies only when snapping a connectible into place
            public const float DIFF_GROUP_SCORE_MODIFIER = 1048576f; //heavily encourages different groups not to... intermingle
            public const float SINGLE_CONNECTION_SCORE_ADDITION = 1048576f; //delays dead end connections like LC until later
            public const float CANT_CONNECT_SCORE_CAP = 10f; //prevents the score from becoming ridiculously high
            public const float SCORE_POTENTIAL_MODIFIER = 0.5f;

            public string name;
            //each Vector2 is relative to the center of the connectible
            public Dictionary<string, Vector2> connLocations;
            public Dictionary<string, string> connections;
            public Vector2 position;
            public string group;
            public float radius;

            //potential connections code
            public bool allConnectionsMade = false;
            public Dictionary<string, List<float>> potentialConnectionScores = new();
            public Dictionary<string, List<string>> potentialConnections = new(); //connections stored in NAME;CONNKEY format

            public Connectible(string name, Dictionary<string, Vector2> connLocations) : this(name, connLocations, new Vector2(0, 0), "none")
            {}
            public Connectible(string name, Dictionary<string, Vector2> connLocations, string group) : this(name, connLocations, new Vector2(0, 0), group)
            { }
            public Connectible(string name, Dictionary<string, Vector2> connLocations, Vector2 position, string group)
            {
                this.name = name;
                this.connLocations = connLocations;
                this.connections = new();
                this.position = position;
                this.group = group;

                float sqrRad = 0;
                foreach (var conn in connLocations)
                {
                    this.connections.Add(conn.Key, "");
                    sqrRad = Mathf.Max(sqrRad, conn.Value.SqrMagnitude());

                    potentialConnectionScores.Add(conn.Key, new());
                    potentialConnections.Add(conn.Key, new());
                }
                this.radius = Mathf.Sqrt(sqrRad);
            }

            public Connectible FreshClone()
            {
                return new Connectible(name, connLocations, position, group);
            }


            public Vector2 WorldPosition(string conn)
            {
                return this.position + connLocations[conn];
            }

            public void MakeConnection(Connectible c, string thisConn, string thatConn)
            {
                this.connections[thisConn] = c.name;
                c.connections[thatConn] = this.name;

                this.allConnectionsMade = true;
                foreach (string v in this.connections.Values)
                {
                    if (v == "")
                    {
                        this.allConnectionsMade = false;
                        break;
                    }
                }
                //clear potential connections info
                if (this.allConnectionsMade)
                {
                    foreach (List<float> l in this.potentialConnectionScores.Values)
                        l.Clear();
                    //this.potentialConnectionScores.Clear();
                    foreach (List<string> l in this.potentialConnections.Values)
                        l.Clear();
                    //this.potentialConnections.Clear();
                }
            }

            //also removes potential connections from other connectibles
            public void MakeConnection(Connectible c, string thisConn, string thatConn, List<Connectible> otherConnectibles)
            {
                this.MakeConnection(c, thisConn, thatConn);

                string connection1 = this.name + ";" + thisConn;
                string connection2 = c.name + ";" + thatConn;

                //remove all instances of the potential connections made (connection1 and connection2)
                foreach (Connectible other in otherConnectibles)
                {
                    foreach (string key in other.potentialConnections.Keys)
                    {
                        int idx = other.potentialConnections[key].IndexOf(connection1);
                        if (idx >= 0)
                        {
                            other.potentialConnections[key].RemoveAt(idx);
                            other.potentialConnectionScores[key].RemoveAt(idx);
                        }

                        idx = other.potentialConnections[key].IndexOf(connection2);
                        if (idx >= 0)
                        {
                            other.potentialConnections[key].RemoveAt(idx);
                            other.potentialConnectionScores[key].RemoveAt(idx);
                        }
                    }
                }

                this.potentialConnections[thisConn].Clear();
                c.potentialConnections[thatConn].Clear();
                this.potentialConnectionScores[thisConn].Clear();
                c.potentialConnectionScores[thatConn].Clear();

                //remove all connections to c in this
                foreach (string conn in this.potentialConnections.Keys)
                {
                    for (int i = this.potentialConnections[conn].Count - 1; i >= 0; i--)
                    {
                        if (this.potentialConnections[conn][i].StartsWith(c.name))
                        {
                            this.potentialConnections[conn].RemoveAt(i);
                            this.potentialConnectionScores[conn].RemoveAt(i);
                        }
                    }
                }

                //remove all connections to this in c
                foreach (string conn in c.potentialConnections.Keys)
                {
                    for (int i = c.potentialConnections[conn].Count - 1; i >= 0; i--)
                    {
                        if (c.potentialConnections[conn][i].StartsWith(this.name))
                        {
                            c.potentialConnections[conn].RemoveAt(i);
                            c.potentialConnectionScores[conn].RemoveAt(i);
                        }
                    }
                }

            }

            public void SnapToConnection(Connectible c, string thisConn, string thatConn)
            {
                this.position = c.WorldPosition(thatConn) - this.connLocations[thisConn];
            }

            //used for connecting one of notConnected to the rest of connected
            public void ConnectToOtherConnectible(Connectible c, string thisConn, string thatConn, List<Connectible> otherConnectibles)
            {
                this.SnapToConnection(c, thisConn, thatConn);
                this.MakeConnection(c, thisConn, thatConn, otherConnectibles);

                //calculate and add potential connection scores
                foreach (Connectible other in otherConnectibles)
                {
                    if (other.name == this.name || other.name == c.name) //don't add itself as a potential connections
                        continue;

                    foreach (string conn in this.connections.Keys)
                    {
                        if (this.connections[conn] != "")
                            continue;
                        foreach (string conn2 in other.connections.Keys)
                        {
                            if (other.connections[conn2] != "")
                                continue;
                            float score = ConnectionScore(other, conn, conn2);
                            this.AddPotentialConnection(conn, other.name + ";" + conn2, score);
                            other.AddPotentialConnection(conn2, this.name + ";" + conn, score);
                        }
                    }
                }
            }


            //potential connections code
            public void AddPotentialConnection(string thisConn, string thatConnection, float score) 
            {
                if (!this.potentialConnections[thisConn].Contains(thatConnection))
                {
                    int idx = -1; //idx of first lower score
                    for (int i = 0; i < potentialConnectionScores[thisConn].Count; i++)
                    {
                        if (potentialConnectionScores[thisConn][i] > score)
                        {
                            idx = i;
                            break;
                        }
                    }

                    if (idx < 0)
                    {
                        this.potentialConnectionScores[thisConn].Add(score);
                        this.potentialConnections[thisConn].Add(thatConnection);
                    } else
                    {
                        this.potentialConnectionScores[thisConn].Insert(idx, score);
                        this.potentialConnections[thisConn].Insert(idx, thatConnection);
                    }
                }
            }

            //thisConnection and thatConnection are the two connections being connected, in form NAME;CONN
            public float ScoreDiffWhenConnectionTaken(string thisConnection, string thatConnection, List<Connectible> otherConnectibles)
            {
                float total = 0;

                //find diff between each potential connection thisConnection and the next potential connection
                foreach (Connectible c in otherConnectibles)
                {
                    foreach (string thatConn in c.potentialConnections.Keys)
                    {
                        string connectionString = c.name + ";" + thatConn;
                        if (connectionString == thisConnection || connectionString == thatConnection) //don't check itself
                            continue;
                        if (c.connections[thatConn] != "") //only check non-connected connections
                            continue;

                        int idx = c.potentialConnections[thatConn].IndexOf(thisConnection);
                        if (idx < 0)
                            continue;
                        if (idx < c.potentialConnectionScores[thatConn].Count - 1)
                        {
                            total += QuickPow(0.5f, idx) * (c.potentialConnectionScores[thatConn][idx + 1] - c.potentialConnectionScores[thatConn][idx]);
                        }
                        else //if there is no secondary connection to replace it
                        {
                            //replaced CANT_CONNECT_SCORE_CAP with float.PositiveInfinity to try to reduce impossible situations
                            //infinity causes problems... instead I'm using CANT_CONNECT_SCORE_CAP squared
                            //total += QuickPow(0.5f, idx) * (CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP - c.potentialConnectionScores[thatConn][idx]);
                            total += QuickPow(0.5f, idx) * CANT_CONNECT_SCORE_CAP;
                        }
                    }
                }

                //(copied from above) find diff between each potential connection thatConnection and the next potential connection
                foreach (Connectible c in otherConnectibles)
                {
                    foreach (string thatConn in c.potentialConnections.Keys)
                    {
                        string connectionString = c.name + ";" + thatConn;
                        if (connectionString == thisConnection || connectionString == thatConnection) //don't check itself
                            continue;
                        if (c.connections[thatConn] != "") //only check non-connected connections
                            continue;

                        int idx = c.potentialConnections[thatConn].IndexOf(thatConnection);
                        if (idx < 0)
                            continue;
                        if (idx < c.potentialConnectionScores[thatConn].Count - 1)
                        {
                            total += QuickPow(0.5f, idx) * (c.potentialConnectionScores[thatConn][idx + 1] - c.potentialConnectionScores[thatConn][idx]);
                        }
                        else //if there is no secondary connection to replace it
                        {
                            //total += QuickPow(0.5f, idx) * (CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP - c.potentialConnectionScores[thatConn][idx]);
                            total += QuickPow(0.5f, idx) * CANT_CONNECT_SCORE_CAP;
                        }
                    }
                }


                string thisConnName = thisConnection.Split(';')[0];
                string thatConnName = thatConnection.Split(';')[0];


                //check whether this connectible will allow other connectibles to connect everything
                bool thisConnectibleHasFreeConns = false;
                foreach (Connectible c in otherConnectibles)
                {
                    if (c.name != thisConnName)
                        continue;
                    List<string> l = c.connections.Values.ToList();
                    int idx = l.IndexOf("");
                    thisConnectibleHasFreeConns = idx >= 0 && l.IndexOf("", idx + 1) > idx;
                    break;
                }
                bool thatConnectibleHasFreeConns = false;
                foreach (Connectible c in otherConnectibles)
                {
                    if (c.name != thatConnName)
                        continue;
                    List<string> l = c.connections.Values.ToList();
                    int idx = l.IndexOf("");
                    thatConnectibleHasFreeConns = idx >= 0 && l.IndexOf("", idx + 1) > idx;
                    break;
                }

                foreach (Connectible c in otherConnectibles)
                {
                    if (c.name == thisConnName || c.name == thatConnName)
                        continue;
                    int freeConnCount = 0;
                    foreach (string s in c.connections.Values)
                    {
                        if (s == "")
                            freeConnCount++;
                    }
                    List<string> potentialRegions = new List<string>();
                    foreach (Connectible c2 in otherConnectibles)
                    {
                        if (c2.name != c.name && c2.connections.ContainsValue("") && !c2.connections.ContainsValue(c.name))
                            potentialRegions.Add(c2.name);
                    }
                    if (!thisConnectibleHasFreeConns)
                        potentialRegions.Remove(thisConnName);
                    if (!thatConnectibleHasFreeConns)
                        potentialRegions.Remove(thatConnName);

                    if (potentialRegions.Count < freeConnCount)
                        total += CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP; //hefty penalty

                    potentialRegions.Clear();
                }

                //consider the limitation that each connectible can only connect to another connectible (c) once
                //does not search EACH connection denied... because I'm too lazy to implement that loop, and it seems over-kill
                foreach (Connectible c in otherConnectibles)
                {
                    if (c.name != thisConnName) //c should = this
                        continue;

                    foreach (string thisConn in c.potentialConnections.Keys)
                    {
                        if (c.connections[thisConn] != "") //only check non-connected connections
                            continue;

                        int idx = c.potentialConnections[thisConn].Count; //idx of first non-c connection
                        for (int i = 0; i < idx; i++)
                        {
                            if (!c.potentialConnections[thisConn][i].StartsWith(thatConnName))
                                idx = i;
                        }
                        if (idx == 0)
                            continue;
                        if (idx < c.potentialConnections[thisConn].Count)
                        {
                            total += c.potentialConnectionScores[thisConn][idx] - c.potentialConnectionScores[thisConn][0];
                        }
                        else //if there is no secondary connection to replace it
                        {
                            //total += CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP - c.potentialConnectionScores[thisConn][0];
                            total += CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP;
                        }
                    }
                    break;
                }

                //exact same but with thatConnection instead
                foreach (Connectible c in otherConnectibles)
                {
                    if (c.name != thatConnName) //c should = this
                        continue;

                    foreach (string thisConn in c.potentialConnections.Keys)
                    {
                        if (c.connections[thisConn] != "") //only check non-connected connections
                            continue;

                        int idx = c.potentialConnections[thisConn].Count; //idx of first non-c connection
                        for (int i = 0; i < idx; i++)
                        {
                            if (!c.potentialConnections[thisConn][i].StartsWith(thisConnName))
                                idx = i;
                        }
                        if (idx == 0)
                            continue;
                        if (idx < c.potentialConnections[thisConn].Count)
                        {
                            total += c.potentialConnectionScores[thisConn][idx] - c.potentialConnectionScores[thisConn][0];
                        }
                        else //if there is no secondary connection to replace it
                        {
                            //total += CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP - c.potentialConnectionScores[thisConn][0];
                            total += CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP;
                        }
                    }
                    break;
                }

                return SCORE_POTENTIAL_MODIFIER * total;
            }

            //returns a non-positive value... hopefully
            public float ScoreBonusWhenConnectionAdded(float score, string thisConn)
            {
                if (score >= CANT_CONNECT_SCORE_CAP)
                    return 0;

                float total = 0;
                int idx = -1;
                for (int i = 0; i < this.potentialConnectionScores[thisConn].Count; i++) {
                    if (this.potentialConnectionScores[thisConn][i] > score)
                    {
                        idx = i;
                        break;
                    }
                }
                if (idx >= 0)
                    total += QuickPow(0.5f, idx) * (score - this.potentialConnectionScores[thisConn][idx]);
                else
                    total += QuickPow(0.5f, idx) * (score - CANT_CONNECT_SCORE_CAP);

                return SCORE_POTENTIAL_MODIFIER * total;
            }

            public float TotalDistanceScore(List<Connectible> otherConnectibles)
            {
                float total = 0;
                foreach (Connectible c in otherConnectibles)
                {
                    if (c.name != this.name)
                        total += this.DistanceScore(c);
                }
                return total;
            }
            public float DistanceScore(Connectible c)
            {
                return DISTANCE_SCORE_MODIFIER * Square(this.radius + c.radius) / (this.position - c.position).SqrMagnitude();
            }

            /**
             * Returns the cheapest connection in connectible c, and that score
             */
            public KeyValuePair<string, float> LowestConnectionScore(Connectible c, string conn, bool allowPotentialBonus = false)
            {
                if (PROHIBIT_DOUBLE_CONNECTIONS && connections.Values.Contains(c.name))
                    return new KeyValuePair<string, float>("", float.PositiveInfinity);

                float lowestScore = float.PositiveInfinity;
                string lowestConn = "";

                foreach (string thatConn in c.connections.Keys)
                {
                    if (c.connections[thatConn] == "") //don't test already-made connections
                    {
                        float score = ConnectionScore(c, conn, thatConn);
                        if (allowPotentialBonus)
                            score += c.ScoreBonusWhenConnectionAdded(score, thatConn);
                        if (score < lowestScore)
                        {
                            lowestScore = score;
                            lowestConn = thatConn;
                        }
                    }
                }

                if (this.group != c.group)
                    lowestScore *= DIFF_GROUP_SCORE_MODIFIER;

                return new KeyValuePair<string, float>(lowestConn, lowestScore);
            }


            public KeyValuePair<string, float> ResolveBestConnection(Connectible c, string thisConn, List<Connectible> otherConnectibles)
            {
                if (PROHIBIT_DOUBLE_CONNECTIONS && connections.Values.Contains(c.name))
                    return new KeyValuePair<string, float>("", float.PositiveInfinity);

                float lowestScore = float.PositiveInfinity;
                string lowestConn = "";

                float scoreCap = (this.group == c.group) ? CANT_CONNECT_SCORE_CAP : CANT_CONNECT_SCORE_CAP * DIFF_GROUP_SCORE_MODIFIER;

                foreach (string thatConn in c.connections.Keys)
                {
                    if (c.connections[thatConn] == "") //don't test already-made connections
                    {
                        c.SnapToConnection(this, thatConn, thisConn);
                        float score = ConnectionScore(c, thisConn, thatConn);
                        score += PrimaryAngleModifier(c, thisConn, thatConn);
                        //score += score - ((c.potentialConnectionScores[thatConn].Count > 0) ? c.potentialConnectionScores[thatConn][0] : CANT_CONNECT_SCORE_CAP);
                        //c is unconnected, so it shouldn't have any potential scores
                        score += score - ((this.potentialConnectionScores[thisConn].Count > 0) ? this.potentialConnectionScores[thisConn][0] : CANT_CONNECT_SCORE_CAP);
                        score += c.TotalDistanceScore(otherConnectibles);

                        //add score for each not-connected connection
                        foreach (string thatConn2 in c.connections.Keys)
                        {
                            if (thatConn2 == thatConn)
                                continue;
                            foreach (Connectible c2 in otherConnectibles)
                            {
                                if (c2.name == c.name || c2.name == this.name)
                                    continue;
                                float score2 = c.LowestConnectionScore(c2, thatConn2, true).Value;

                                if (score2 > scoreCap)
                                    score2 = scoreCap;
                                score += score2;
                            }
                        }

                        if (score < lowestScore)
                        {
                            lowestScore = score;
                            lowestConn = thatConn;
                        }
                    }
                }

                lowestScore += 0.25f; //this hopefully prevents negative scores from popping up

                if (c.connections.Count < 2)
                    lowestScore += SINGLE_CONNECTION_SCORE_ADDITION;

                if (this.group != c.group)
                    lowestScore *= DIFF_GROUP_SCORE_MODIFIER;

                //divide score for larger connectibles
                lowestScore *= Mathf.Pow(CANT_CONNECT_SCORE_CAP, 1 - this.connections.Count);

                return new KeyValuePair<string, float>(lowestConn, lowestScore);
            }

            public float ConnectionScore(Connectible c, string thisConn, string thatConn)
            {
                //return (this.WorldPosition(conn1) - c.WorldPosition(conn2)).SqrMagnitude() + EST_DIST_SQR * (this.connLocations[conn1].normalized - c.connLocations[conn2].normalized).SqrMagnitude();
                return CONNECTION_DISTANCE_MODIFIER * (this.WorldPosition(thisConn) - c.WorldPosition(thatConn)).magnitude / (this.radius + c.radius)
                    + ANGLE_SCORE_MODIFIER * (this.connLocations[thisConn].normalized + c.connLocations[thatConn].normalized).SqrMagnitude() //connLoc angle vs. connLoc angle
                    + PLACEMENT_ANGLE_MODIFIER * (this.connLocations[thisConn].normalized - (c.position - this.position).normalized).SqrMagnitude() //connLoc angle vs. position difference
                    + PLACEMENT_ANGLE_MODIFIER * (c.connLocations[thatConn].normalized - (this.position - c.position).normalized).SqrMagnitude()
                    + PLACEMENT_ANGLE_MODIFIER * (this.connLocations[thisConn].normalized - (c.WorldPosition(thatConn) - this.position).normalized).SqrMagnitude() //connLoc angle vs. actual angle (0 when snapped to connection)
                    + PLACEMENT_ANGLE_MODIFIER * (c.connLocations[thatConn].normalized - (this.WorldPosition(thisConn) - c.position).normalized).SqrMagnitude()
                    + PLACEMENT_ANGLE_MODIFIER * (this.connLocations[thisConn].normalized - (c.position - this.WorldPosition(thisConn)).normalized).SqrMagnitude() //connLoc angle vs. connLoc to c.position
                    + PLACEMENT_ANGLE_MODIFIER * (c.connLocations[thatConn].normalized - (this.position - c.WorldPosition(thatConn)).normalized).SqrMagnitude();
            }

            public float PrimaryAngleModifier(Connectible c, string thisConn, string thatConn)
            {
                return BONUS_ANGLE_PLACEMENT_MODIFIER * (this.connLocations[thisConn].normalized + c.connLocations[thatConn].normalized).SqrMagnitude();
            }
        }
        #endregion

        //get room positions
        #region Get_Room_Map_Positions
        public static Dictionary<string, Vector2> GetRoomMapPositions(string region, List<string> roomNames, string slugcat = "")
        {
            Dictionary<string, Vector2> dict = new();
            List<string> rooms = roomNames.ToArray().ToList();

            //look through map file and copy coordinates if present
            string mapPath = GetRegionMapFile(region, slugcat);

            if (!File.Exists(mapPath))
            {
                //if no map data, just randomize it!
                foreach (string room in rooms)
                {
                    dict.Add(room, Custom.RNV() * UnityEngine.Random.value * 1000f);
                }
                return dict;
            }

            //read map data
            string[] mapLines = File.ReadAllLines(mapPath);

            for (int i = rooms.Count - 1; i >= 0; i--)
            {
                Vector2 pos = GetMapPositionOfRoom(mapLines, rooms[i]);
                if (!float.IsInfinity(pos.x))
                {
                    dict.Add(rooms[i], pos);
                    rooms.RemoveAt(i);
                }
            }

            //that should handle most room; certainly all vanilla ones
            //however, some modded room might not have map data in vanilla regions
            //so we will determine what mapped rooms the modded rooms connect to, and we will use the map locations of THESE rooms
            if (rooms.Count > 0)
            {
                //get connection data
                string worldPath = AssetManager.ResolveFilePath(string.Concat(new string[]
                {
                    "World",
                    Path.DirectorySeparatorChar.ToString(),
                    region,
                    Path.DirectorySeparatorChar.ToString(),
                    "world_",
                    region,
                    ".txt"
                }));

                if (!File.Exists(worldPath))
                {
                    //if no world data, just randomize it!
                    foreach (string room in rooms)
                    {
                        dict.Add(room, Custom.RNV() * UnityEngine.Random.value * 1000f);
                    }
                    return dict;
                }

                //read world data
                string[] worldLines = File.ReadAllLines(worldPath);

                roomsSearched.Clear();
                roomsSearched.Add("DISCONNECTED");
                for (int i = rooms.Count - 1; i >= 0; i--)
                {
                    roomsSearched.Add(rooms[i]);

                    //use a dedicated function to recursively search through rooms
                    
                    Vector2 pos = RecursivelySearchForMapPos(mapLines, worldLines, rooms[i]);
                    if (!float.IsInfinity(pos.x))
                        dict.Add(rooms[i], pos);
                    else
                        dict.Add(rooms[i], Custom.RNV() * UnityEngine.Random.value * 1000f);
                    rooms.RemoveAt(i);
                }
                roomsSearched.Clear();
            }

            return dict;
        }

        private static Vector2 NULL_VECTOR2 = new Vector2(float.PositiveInfinity, float.PositiveInfinity);

        private static List<string> roomsSearched = new();
        private static Vector2 RecursivelySearchForMapPos(string[] mapLines, string[] worldLines, string room, int count = 1)
        {
            if (count > 10)
                return NULL_VECTOR2;

            //check if it's on the map
            Vector2 mapPos = GetMapPositionOfRoom(mapLines, room);
            if (!float.IsInfinity(mapPos.x))
            {
                return mapPos + Custom.RNV() * UnityEngine.Random.value * 50f * count;
            }

            roomsSearched.Add(room);

            List<string> conns = GetRoomConnections(worldLines, room, roomsSearched);
            foreach (string c in conns)
            {
                Vector2 pos = RecursivelySearchForMapPos(mapLines, worldLines, c, count + 1);
                if (!float.IsInfinity(pos.x))
                {
                    if (count <= 1)
                        RegionRandomizer.LogSomething("Found gate through connections: " + room + ": " + pos.ToString());
                    return pos;
                }
            }
            if (conns.Count < 1)
                RegionRandomizer.LogSomething("No unsearched room connections for " + room);

            return NULL_VECTOR2;
        }

        private static Vector2 GetMapPositionOfRoom(string[] lines, string room)
        {
            foreach (string line in lines)
            {
                if (line.StartsWith(room))
                {
                    string l = line.Substring(line.IndexOf(':') + 2);
                    string[] s = Regex.Split(l, "><");
                    if (s.Length > 1)
                    {
                        return new Vector2(float.Parse(s[0]), float.Parse(s[1]));
                    }
                }
            }

            return NULL_VECTOR2;
        }

        private static List<string> GetRoomConnections(string[] lines, string room, List<string> excludeRooms = null)
        {
            string connectionData = "";
            bool foundRooms = false;
            foreach (string line in lines)
            {
                if (!foundRooms)
                {
                    if (line.StartsWith("ROOMS"))
                        foundRooms = true;
                }
                else
                {
                    if (line.StartsWith(room))
                    {
                        connectionData = line;
                        break;
                    }
                    else if (line.StartsWith("END ROOMS"))
                        break;
                }
            }

            if (connectionData == "")
                return new List<string>();

            string[] sections = Regex.Split(connectionData, " : ");
            if (sections.Length < 2)
                return new List<string>();

            List<string> list = Regex.Split(sections[1], ", ").ToList();
            if (excludeRooms != null)
            {
                foreach (string r in excludeRooms)
                    list.Remove(r);
            }

            return list;
        }

        private static string GetRegionMapFile(string region, string slugcat)
        {
            string mapPath = AssetManager.ResolveFilePath(string.Concat(new string[]
            {
                "World",
                Path.DirectorySeparatorChar.ToString(),
                region,
                Path.DirectorySeparatorChar.ToString(),
                "map_",
                region,
                "-",
                slugcat,
                ".txt"
            }));
            if (File.Exists(mapPath))
                return mapPath;

            return AssetManager.ResolveFilePath(string.Concat(new string[]
            {
                "World",
                Path.DirectorySeparatorChar.ToString(),
                region,
                Path.DirectorySeparatorChar.ToString(),
                "map_",
                region,
                ".txt"
            }));
        }
        #endregion

        private static float Square(float x)
        {
            return x * x;
        }
        private static float Cube(float x)
        {
            return x * x * x;
        }
        private static float QuickPow(float x, int pow)
        {
            float v = 1;
            for (int i = 0; i < pow; i++)
                v *= x;
            return v;
        }
        private static float Lerp(float t, float a, float b)
        {
            return a + (b - a) * t;
        }
    }
}
