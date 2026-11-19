using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        int coastDestination = -1;

        int NearbyCoastStation()
        {
            if (coast == null || IsVisitor || loading) return -1;
            foreach (int zone in new[] { 2, 4, 5 })
                if (Vector3.Distance(Game.actor.position, DawnCoast.ZonePoints[zone]) < 2.45f) return zone;
            return -1;
        }

        public bool TryNearbyCoastStation()
        {
            int station = NearbyCoastStation();
            if (station < 0) return false;
            coastDestination = -1;
            if (station == 2) OpenChat();
            else if (station == 4) StartCoroutine(TravelFromFerry());
            else OpenStarStation();
            return true;
        }

        IEnumerator TravelFromFerry()
        {
            yield return LoadNeighbors();
            var neighbor = Array.Find(neighborWorld.residents ?? Array.Empty<Resident>(), r => r.id == "ajin" && r.planet == 0);
            if (neighbor == null) { Toast("渡口暂时没有连接到暖焰营地。"); yield break; }
            yield return Visit(0, neighbor.continent);
        }

        void OpenStarStation()
        {
            OpenWorld();
            if (!WorldOpen) return;
            stars = true;
            planetRoot.SetActive(false);
            starRoot.SetActive(true);
        }

        void GoToCoastDistrict(int index)
        {
            if (coast == null || IsVisitor) return;
            if (homecomingJournal) ToggleHomecomingJournal();
            if (NearbyCoastStation() == index) { TryNearbyCoastStation(); return; }
            if (Game.WalkTo(DawnCoast.ZonePoints[index]))
            {
                coastDestination = index;
                Game.orbit.FocusPlayer();
                Toast("前往" + DawnCoast.ZoneNames[index]);
            }
            else Toast("这里暂时走不到，请沿小路前进。");
        }

        void UpdateCoastDestination()
        {
            if (coastDestination < 0) return;
            if (coast == null || loading || IsVisitor || Game == null) { coastDestination = -1; return; }
            var keys = Keyboard.current;
            if (keys != null && (keys.wKey.isPressed || keys.aKey.isPressed || keys.sKey.isPressed || keys.dKey.isPressed || keys.escapeKey.wasPressedThisFrame))
            { coastDestination = -1; return; }
            if (Game.Navigating || Game.Busy) return;
            var station = coastDestination;
            coastDestination = -1;
            if (NearbyCoastStation() == station) TryNearbyCoastStation();
        }

        void DrawCoastPrompt()
        {
            if (homecomingJournal || Game.Busy || Game.Navigating || Game.expansion.BlocksInput) return;
            var station = NearbyCoastStation();
            string label = station switch
            {
                2 => "共创工坊 · 和小禾聊聊  [E]",
                4 => "渡口 · 拜访阿烬  [E]",
                5 => "星图台 · 查看其他星球  [E]",
                _ => null
            };
            if (label != null && GUI.Button(new Rect(490,605,300,44), label, button)) TryNearbyCoastStation();
        }
    }
}
