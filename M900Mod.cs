using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using GHPC.Weapons;
using GHPC.Vehicle;
using GHPC.Camera;
using GHPC.Player;
using GHPC.Equipment.Optics;
using System.Collections;
using System.Threading.Tasks;
using GHPC.Weaponry;

namespace M900
{
    public class M900Mod : MelonMod
    {
        MelonPreferences_Category cfg;
        MelonPreferences_Entry<int> m900Count;
        MelonPreferences_Entry<bool> rotateAzimuth;

        GameObject[] vic_gos;
        GameObject gameManager;
        CameraManager cameraManager;
        PlayerInput playerManager;

        AmmoClipCodexScriptable clip_codex_m900;
        AmmoType.AmmoClip clip_m900;
        AmmoCodexScriptable ammo_codex_m900;
        AmmoType ammo_m900;

        AmmoType ammo_hvap;

        public static void ShallowCopy(System.Object dest, System.Object src)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo[] destFields = dest.GetType().GetFields(flags);
            FieldInfo[] srcFields = src.GetType().GetFields(flags);

            foreach (FieldInfo srcField in srcFields)
            {
                FieldInfo destField = destFields.FirstOrDefault(field => field.Name == srcField.Name);
                if (destField != null && !destField.IsLiteral)
                {
                    if (srcField.FieldType == destField.FieldType)
                        destField.SetValue(dest, srcField.GetValue(src));
                }
            }
        }

        public static void EmptyRack(GHPC.Weapons.AmmoRack rack)
        {
            MethodInfo removeVis = typeof(GHPC.Weapons.AmmoRack).GetMethod("RemoveAmmoVisualFromSlot", BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo stored_clips = typeof(GHPC.Weapons.AmmoRack).GetProperty("StoredClips");
            stored_clips.SetValue(rack, new List<AmmoType.AmmoClip>());
            rack.SlotIndicesByAmmoType = new Dictionary<AmmoType, List<byte>>();

            foreach (Transform transform in rack.VisualSlots)
            {
                AmmoStoredVisual vis = transform.GetComponentInChildren<AmmoStoredVisual>();
                if (vis != null && vis.AmmoType != null)
                {
                    removeVis.Invoke(rack, new object[] { transform });
                }
            }
        }

        public override void OnUpdate()
        {
            if (gameManager == null) return;

            FieldInfo currentCamSlot = typeof(CameraManager).GetField("_currentCamSlot", BindingFlags.Instance | BindingFlags.NonPublic);
            CameraSlot cam = (CameraSlot)currentCamSlot.GetValue(cameraManager);

            if (cam == null || cam.name != "Aux sight (GAS)") return;
            if (playerManager.CurrentPlayerWeapon.Name != "85mm DT-5") return;

            AmmoType currentAmmo = playerManager.CurrentPlayerWeapon.FCS.CurrentAmmoType;
            int reticleId = (currentAmmo.Name == "M900 APFSDS-T") ? 0 : 2;

            GameObject reticle = cam.transform.GetChild(reticleId).gameObject;
            if (!reticle.activeSelf)
            {
                reticle.SetActive(true);
            }
        }

        public override async void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (sceneName == "LOADER_INITIAL" || sceneName == "MainMenu2_Scene" || sceneName == "t64_menu") return;

            vic_gos = GameObject.FindGameObjectsWithTag("Vehicle");

            while (vic_gos.Length == 0)
            {
                vic_gos = GameObject.FindGameObjectsWithTag("Vehicle");
                await Task.Delay(3000);
            }

            if (ammo_m900 == null)
            {
                foreach (AmmoCodexScriptable s in Resources.FindObjectsOfTypeAll(typeof(AmmoCodexScriptable)))
                {
                    if (s.AmmoType.Name == "85mm BR-365P HVAP")
                    {
                        ammo_hvap = s.AmmoType;
                    }
                }

                ammo_m900 = new AmmoType();
                ShallowCopy(ammo_m900, ammo_hvap);
                ammo_m900.Name = "M900 APFSDS-T";
                ammo_m900.Caliber = 85;
                ammo_m900.RhaPenetration = 540;
                ammo_m900.MuzzleVelocity = 1500f;
                ammo_m900.Mass = 4.2f;

                ammo_codex_m900 = ScriptableObject.CreateInstance<AmmoCodexScriptable>();
                ammo_codex_m900.AmmoType = ammo_m900;
                ammo_codex_m900.name = "ammo_m900";

                clip_m900 = new AmmoType.AmmoClip();
                clip_m900.Capacity = 1;
                clip_m900.Name = "M900 APFSDS-T";
                clip_m900.MinimalPattern = new AmmoCodexScriptable[1] { ammo_codex_m900 };

                clip_codex_m900 = ScriptableObject.CreateInstance<AmmoClipCodexScriptable>();
                clip_codex_m900.name = "clip_m900";
                clip_codex_m900.ClipType = clip_m900;
            }

            foreach (GameObject vic_go in vic_gos)
            {
                Vehicle vic = vic_go.GetComponent<Vehicle>();
                if (vic == null) continue;

                if (vic.FriendlyName == "T-34-85M")
                {
                    gameManager = GameObject.Find("_APP_GHPC_");
                    cameraManager = gameManager.GetComponent<CameraManager>();
                    playerManager = gameManager.GetComponent<PlayerInput>();

                    GameObject ammo_m900_vis = GameObject.Instantiate(ammo_hvap.VisualModel);
                    ammo_m900_vis.name = "m900 visual";
                    ammo_m900.VisualModel = ammo_m900_vis;
                    ammo_m900.VisualModel.GetComponent<AmmoStoredVisual>().AmmoType = ammo_m900;
                    ammo_m900.VisualModel.GetComponent<AmmoStoredVisual>().AmmoScriptable = ammo_codex_m900;

                    WeaponsManager weaponsManager = vic.GetComponent<WeaponsManager>();
                    WeaponSystem mainGun = weaponsManager.Weapons[0].Weapon;

                    LoadoutManager loadoutManager = vic.GetComponent<LoadoutManager>();
                    loadoutManager.LoadedAmmoTypes[0] = clip_codex_m900;

                    for (int i = 0; i < loadoutManager.RackLoadouts.Length; i++)
                    {
                        GHPC.Weapons.AmmoRack rack = loadoutManager.RackLoadouts[i].Rack;
                        rack.ClipTypes[0] = clip_m900;
                        EmptyRack(rack);
                    }

                    loadoutManager.SpawnCurrentLoadout();

                    PropertyInfo roundInBreech = typeof(AmmoFeed).GetProperty("AmmoTypeInBreech");
                    roundInBreech.SetValue(mainGun.Feed, null);

                    MethodInfo refreshBreech = typeof(AmmoFeed).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
                    refreshBreech.Invoke(mainGun.Feed, new object[] { });

                    MethodInfo registerAllBallistics = typeof(LoadoutManager).GetMethod("RegisterAllBallistics", BindingFlags.Instance | BindingFlags.NonPublic);
                    registerAllBallistics.Invoke(loadoutManager, new object[] { });
                }
            }
        }
    }
}
