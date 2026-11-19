using UnityEngine;
using UnityEngine.InputSystem;

namespace DawnGarden
{
    public sealed class GardenOrbitCamera : MonoBehaviour
    {
        public Transform target;
        public DawnGardenGame game;
        public float yaw = 155f, pitch = 37f, distance = 12.5f;
        public bool overview;
        public bool largeWorld;
        public bool overviewOrthographic,overviewPerspective;
        public bool placementView;
        public float overviewFieldOfView=20;
        public float overviewSize=16.9f, overviewYaw=0, overviewPitch=25.115f;
        public Transform sightObstruction;
        public Vector3 expandedOverviewCenter = new Vector3(2.7f, .5f, 0);
        public float expandedOverviewDistance = 22f, expandedMaxDistance = 27f;
        Vector3 center;
        bool ready, cinematic;
        Vector3 cinematicCenter;
        float cinematicShake;
        readonly RaycastHit[] sightHits = new RaycastHit[16];
        float unobstructedDistance = float.PositiveInfinity;
        public void FrameExpansion(Vector3 focus, float shake) { cinematic = true; cinematicCenter = focus; cinematicShake = shake; }
        public void EndExpansionView() { cinematic = false; cinematicShake = 0; overview = true; distance = expandedOverviewDistance; yaw = (overviewOrthographic||overviewPerspective) ? overviewYaw : 162; pitch = (overviewOrthographic||overviewPerspective) ? overviewPitch : 43; }
        public void FramePlacement(Vector3 plotCenter, bool close = false)
        {
            // The continent overview uses a long lens; editing one plot needs its own framing.
            placementView = true; overview = true; cinematic = false;
            yaw = -25; pitch = 52; distance = close ? 14 : 19;
            expandedOverviewCenter = plotCenter + Vector3.up * .4f +
                Quaternion.Euler(0, yaw, 0) * Vector3.right * (close ? 2.2f : 3f);
            center = expandedOverviewCenter; ready = true;
        }
        void LateUpdate()
        {
            var lens=GetComponent<Camera>();
            lens.orthographic=!placementView && overviewOrthographic && overview && !cinematic;
            if(placementView)lens.fieldOfView=43;
            else if(overviewPerspective)lens.fieldOfView=overview&&!cinematic?overviewFieldOfView:43;
            if(lens.orthographic)lens.orthographicSize=overviewSize*distance/expandedOverviewDistance;
            var mouse = Mouse.current;
            if (!cinematic && mouse != null && !game.PointerOverHud(mouse.position.ReadValue()))
            {
                if (mouse.rightButton.isPressed)
                {
                    var delta = mouse.delta.ReadValue();
                    yaw += delta.x * .18f;
                    pitch = Mathf.Clamp(pitch - delta.y * .14f, 22f, 67f);
                }
                distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * .008f * (largeWorld && overview ? 3 : 1), 5.2f,
                    largeWorld || (game.Progress != null && game.Progress.firstExpansionUnlocked) ? expandedMaxDistance : 17f);
            }
            var desired = cinematic ? cinematicCenter : overview ? (largeWorld || (game.Progress != null && game.Progress.firstExpansionUnlocked) ? expandedOverviewCenter : new Vector3(0, .5f, 0)) : target.position + Vector3.up * .7f;
            if (cinematic) { yaw = Mathf.LerpAngle(yaw, 162, 1 - Mathf.Exp(-2 * Time.deltaTime)); pitch = Mathf.Lerp(pitch, 43, 1 - Mathf.Exp(-2 * Time.deltaTime)); distance = Mathf.Lerp(distance, largeWorld ? 22f : expandedOverviewDistance, 1 - Mathf.Exp(-2 * Time.deltaTime)); }
            if (!ready) { center = desired; ready = true; }
            center = Vector3.Lerp(center, desired, 1 - Mathf.Exp(-5f * Time.deltaTime));
            var direction = Quaternion.Euler(pitch, yaw, 0) * Vector3.back;
            float visibleDistance = distance;
            if (largeWorld && !overview && !cinematic)
            {
                int count = Physics.SphereCastNonAlloc(center,.24f,direction,sightHits,distance,
                    (1<<8)|(1<<9),QueryTriggerInteraction.Ignore);
                for (int i=0; i<count; i++)
                {
                    var hit = sightHits[i];
                    if (hit.collider.transform.IsChildOf(target)) continue;
                    visibleDistance = Mathf.Min(visibleDistance,Mathf.Max(.35f,hit.distance-.24f));
                }
                if (sightObstruction != null)
                {
                    var hull = sightObstruction.GetComponent<Collider>();
                    if (hull != null && hull.Raycast(new Ray(center,direction),out var hit,distance))
                        visibleDistance = Mathf.Min(visibleDistance,Mathf.Max(.35f,hit.distance-.38f));
                }
                if (visibleDistance > unobstructedDistance)
                    visibleDistance = Mathf.MoveTowards(unobstructedDistance,visibleDistance,18*Time.deltaTime);
                unobstructedDistance = visibleDistance;
            }
            else unobstructedDistance = float.PositiveInfinity;
            transform.position = center + direction * visibleDistance;
            transform.LookAt(center);
            if (cinematic) transform.position += new Vector3(Mathf.Sin(Time.time*31), Mathf.Cos(Time.time*37), 0)*cinematicShake;
        }
        public void ToggleOverview() { overview = !overview; if(overview && (overviewOrthographic||overviewPerspective)){yaw=overviewYaw;pitch=overviewPitch;} distance = overview ? (largeWorld || (game.Progress != null && game.Progress.firstExpansionUnlocked) ? expandedOverviewDistance : 15.5f) : 9f; }
        public void FocusPlayer() { overview = false; distance = 7.6f; }
    }
}
