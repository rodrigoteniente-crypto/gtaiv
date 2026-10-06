using System;
using System.Numerics;
using static KickChaos.N;

namespace KickChaos
{
    public partial class SubNpcManager
    {
        public bool AdminAction(StreamAdminCommand command, out string message)
        {
            Gang gang;
            Member member = FindStreamMember(command.NpcId, out gang);
            message = "El NPC ya no está vivo";
            if (member == null || member.Dead || member.Ped == 0 || !DOES_CHAR_EXIST(member.Ped) || IS_CHAR_DEAD(member.Ped) || IS_CHAR_FATALLY_INJURED(member.Ped)) return false;
            double now = G.Now;
            switch (command.Action)
            {
                case "build":
                    RogueBuild build;
                    if (!NpcRoguePolicy.ParseBuild(command.Value, out build)) { message = "Build desconocida"; return false; }
                    SetRogueBuild(member, build, now, "panel");
                    message = "Build aplicada a " + Name(gang, member); return true;
                case "behavior":
                    NpcBehavior? behavior = NpcRules.Parse(command.Value);
                    if (!behavior.HasValue) { message = "Comportamiento desconocido"; return false; }
                    gang.LastModeSwitch = -100;
                    SetMode(gang, behavior.Value, "panel", now);
                    message = "Comportamiento aplicado a la banda"; return true;
                case "relocate":
                case "teleport":
                    Vector3 destination = new Vector3(command.X, command.Y, command.Z);
                    if (command.Action == "relocate") destination = member.Pos + new Vector3(5, 3, 1);
                    else if (command.Zone.Length > 0)
                    {
                        if (command.Zone == "cámara") destination = dir.ActionCenter();
                        else if (command.Zone == "Broker") destination = new Vector3(1014, -460, 20);
                        else if (command.Zone == "Algonquin") destination = new Vector3(15, 205, 14);
                        else if (command.Zone == "Alderney") destination = new Vector3(-1060, 755, 13);
                        else if (command.Zone == "Bohan") destination = new Vector3(425, 1650, 17);
                        else { message = "Zona desconocida"; return false; }
                    }
                    Vector3 safe;
                    if (!GET_SAFE_POSITION_FOR_CHAR(destination, true, out safe) || !StreamWire.ValidPosition(safe.X, safe.Y, safe.Z))
                    { message = "No hay una posición segura cargada en esa zona; acercá primero la cámara"; return false; }
                    AdminStopEvents(member);
                    if (IS_CHAR_IN_ANY_CAR(member.Ped)) WARP_CHAR_FROM_CAR_TO_COORD(member.Ped, safe);
                    else SET_CHAR_COORDINATES(member.Ped, safe);
                    CLEAR_CHAR_TASKS(member.Ped);
                    member.Pos = safe; member.Mode = M_NONE; member.Phase = P_NONE; member.Target = 0;
                    member.InCar = false; member.Driving = false; member.Car = 0; member.StealCar = 0; member.StealSince = -1;
                    member.Seat = -1; member.HopAt = -1; member.DriveSince = -1; member.LastCarCheck = -100;
                    member.StillSince = -1; member.NextThink = now; member.LastTask = -100; member.Fails = 0;
                    member.ModeAt = now; member.RoomKey = 0; member.LastRoom = -100; member.Head = safe + new Vector3(0, 0, 1);
                    member.Fleeing = false; member.Eating = false; member.EventKind = 0; member.EventUntil = -1;
                    gang.Center = safe; gang.LastWarp = now;
                    Changed();
                    message = "NPC reubicado: conserva vida, kills y mejoras"; return true;
                case "kill":
                case "remove":
                    AdminStopEvents(member);
                    SET_CHAR_INVINCIBLE(member.Ped, false);
                    SET_CHAR_PROOFS(member.Ped, false, false, false, false, false);
                    SET_CHAR_HEALTH(member.Ped, 0);
                    OnMemberDeath(gang, member, now);
                    if (command.Action == "remove")
                    {
                        int ped = member.Ped;
                        if (member.FakeName) REMOVE_FAKE_NETWORK_NAME_FROM_PED(ped);
                        DELETE_CHAR(ped); G.ProtectedPeds.Remove(ped); member.Ped = 0;
                    }
                    Changed(); message = command.Action == "kill" ? "NPC muerto" : "NPC eliminado"; return true;
                case "debug":
                    Diagnose(gang, member, now);
                    message = Name(gang, member) + " | id " + member.RowId + " | ped " + member.Ped + " | " + ModeName(member.Mode) +
                        " | objetivo " + member.Target + " | tarea hace " + Math.Max(0, now - member.LastTask).ToString("0.0") + "s" +
                        " | auto " + member.Car + " | IA " + (nativeAi ? "Juego" : "Mod");
                    return true;
            }
            message = "Acción de NPC desconocida"; return false;
        }
        void AdminStopEvents(Member member)
        {
            if (member.EventKind == 1) EndChat(member, "administración");
            if (member.EventKind == 2) ReleaseRacer(member);
            if (convoy != null && (convoy.Leader == member || convoy.Follower == member)) EndConvoy("administración");
        }
    }
}
