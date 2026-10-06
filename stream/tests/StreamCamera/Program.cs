using System.Numerics;
using KickChaos;

int checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
void Close(double actual, double expected, string message) { Check(Math.Abs(actual - expected) < 0.000001, message); }

// 300s city shot interrupted at 93.5s by a 15s sub view, then a 40s donation.
double elapsed = CityCameraClock.Elapsed(193.5, 100);
Close(CityCameraClock.Remaining(300, elapsed), 206.5, "snapshot preserves remaining time");
double resumed = CityCameraClock.ResumeStart(255, elapsed);
Close(CityCameraClock.Elapsed(255, resumed), 93.5, "resume exact animated-shot progress");
Close(CityCameraClock.Remaining(300, CityCameraClock.Elapsed(260, resumed)), 201.5, "clock continues after return");
for (int n = 0; n < 3000; n++)
{
    double pause = n * 0.1, returnedAt = 500 + n * 13.7;
    double start = CityCameraClock.ResumeStart(returnedAt, pause);
    Close(CityCameraClock.Elapsed(returnedAt, start), pause, "all city offsets restore");
}

var policy = new CameraInterruptionPolicy();
policy.Reset(0);
Check(!policy.Evaluate(19.99, 0, true), "first evaluation waits 20s");
Check(!policy.Evaluate(20, 0.31, true), "31% roll stays quiet");
Check(!policy.Evaluate(21, 0, true), "no per-frame reroll");
Check(policy.Evaluate(40, 0.29, true), "29% roll interrupts");
Check(policy.Begin(40, 10, 10, false, false), "ambient accepted");
Check(!policy.Expired(100), "loading does not spend visible duration");
policy.Visible(100);
Check(!policy.Expired(109.99) && policy.Expired(110), "visible duration begins after loading");
Check(!policy.CanAccept(5, false, false), "lower priority cannot steal view");
Check(policy.Begin(107, 30, 70, false, false), "donation replaces ambient");
policy.Visible(108);
Close(policy.Until, 138, "higher-priority view receives its own duration");
policy.Finish(138);
Check(!policy.Evaluate(160, 0, true), "cooldown persists after return");
Check(policy.Evaluate(260, 0, true), "cooldown becomes available");
Check(!policy.CanAccept(70, false, true), "donation respects manual pin");
Check(policy.CanAccept(60, true, true), "intentional chat camera can interrupt manual pin");
Check(policy.Begin(270, 10, 60, true, true), "manual command accepted");
policy.Visible(271);
policy.Finish(274); // death/explicit return
Check(!policy.Active && policy.AutomaticReadyAt >= 394, "death releases observation and preserves cooldown");
policy.Reset(500);
Check(!policy.Active && !policy.Evaluate(501, 0, true), "load/reset clears old observation");

var quiet = new StreamNpcRow { Ped = 1, Health = 100, X = 1000 };
var nearby = new StreamNpcRow { Ped = 2, Health = 100, X = 5 };
var battle = new StreamNpcRow { Ped = 3, Health = 25, Combat = true, Pursuit = true, Racing = true, RecentUntil = 20, X = 5 };
Check(StreamCameraCandidates.Weight(nearby, 10, 0, 0) > StreamCameraCandidates.Weight(quiet, 10, 0, 0), "nearby actors preferred");
Check(StreamCameraCandidates.Weight(battle, 10, 0, 0) > StreamCameraCandidates.Weight(nearby, 10, 0, 0), "combat/race/recent/low-health preferred");
battle.Health = 0;
Close(StreamCameraCandidates.Weight(battle, 10, 0, 0), 0, "dead actor excluded");
nearby.CameraAvailable = false;
Close(StreamCameraCandidates.Weight(nearby, 10, 0, 0), 0, "temporarily obstructed actor excluded");
Check(FollowCameraGeometry.Usable(new Vector3(4, 0, 1), Vector3.Zero), "close tunnel offset accepted");
Check(FollowCameraGeometry.Usable(new Vector3(22, 0, 6), Vector3.Zero), "ordinary following shot accepted");
Check(!FollowCameraGeometry.Usable(new Vector3(1, 0, 90), Vector3.Zero), "roof/floor shot rejected");
Check(!FollowCameraGeometry.Usable(new Vector3(float.NaN, 2, 4), Vector3.Zero), "invalid pose rejected");

// Even a successful random draw every time remains quiet for most of three hours.
var rhythm = new CameraInterruptionPolicy();
rhythm.Reset(0);
int showing = 0, views = 0;
for (int second = 0; second < 10800; second++)
{
    if (rhythm.Expired(second)) rhythm.Finish(second);
    if (rhythm.Evaluate(second, 0, !rhythm.Active))
    {
        rhythm.Begin(second, 10, 10, false, false);
        rhythm.Visible(second);
        views++;
    }
    if (rhythm.Active) showing++;
}
Check(showing < 1080 && views < 90, "worst-case automatic draw leaves over 90% city time");
Console.WriteLine($"StreamCamera: {checks} checks passed.");
