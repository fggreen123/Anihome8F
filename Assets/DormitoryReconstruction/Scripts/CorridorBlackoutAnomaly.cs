using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Anihome.Dormitory
{
    [DisallowMultipleComponent]
    public sealed class CorridorBlackoutAnomaly : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float chanceOnAnomalyFloor = 0.5f;
        [SerializeField] Transform corridorStart;
        [SerializeField] Transform corridorEnd;
        [SerializeField] AudioClip flickerClip;
        [SerializeField] Material[] lampMaterials;

        const string FireAlarmLight = "Fire_Alarm_Light";
        const float BlackoutEnvironmentBrightness = 0.12f;
        const float NearSurfaceLightRange = 1.5f;
        const float NearSurfaceLightIntensity = 0.065f;
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        readonly List<(Light light, float intensity)> lights = new List<(Light, float)>();
        readonly List<(Light light, float intensity)> fillLights = new List<(Light, float)>();
        readonly List<(Renderer renderer, int slot, Color emission, MaterialPropertyBlock original)> lamps =
            new List<(Renderer, int, Color, MaterialPropertyBlock)>();
        MaterialPropertyBlock scratch;
        Transform player;
        Vector3 center;
        Vector3 corridorAxis;
        bool armed;
        Coroutine flicker;
        AudioSource flickerAudio;
        Light nearSurfaceLight;
        bool environmentDimmed;
        (Color sky, Color equator, Color ground, float reflections) originalEnvironment;

        internal void BeginFloor(bool anomalyFloor, Transform floorPlayer, Transform spawnPoint)
        {
            ResetFloor();
            if (!anomalyFloor || !floorPlayer || !spawnPoint || !corridorStart || !corridorEnd) return;
            if (chanceOnAnomalyFloor <= 0f || (chanceOnAnomalyFloor < 1f && Random.value >= chanceOnAnomalyFloor)) return;

            player = floorPlayer;
            center = (corridorStart.position + corridorEnd.position) * 0.5f;
            center.y = spawnPoint.position.y;
            corridorAxis = Vector3.ProjectOnPlane(corridorEnd.position - corridorStart.position, Vector3.up).normalized;
            armed = corridorAxis.sqrMagnitude > 0f;
        }

        void Update()
        {
            var manager = FloorLoopManager.Instance;
            if (!armed || !player || !manager || manager.Busy || manager.Cleared) return;

            Vector3 offset = player.position - center;
            float along = Vector3.Dot(offset, corridorAxis);
            Vector3 across = offset - corridorAxis * along;
            if (Mathf.Abs(along) > 1.1f || across.sqrMagnitude > 1.1f * 1.1f) return;

            armed = false;
            CaptureLighting();
            PlayFlickerSound();
            flicker = StartCoroutine(Flicker());
        }

        void CaptureLighting()
        {
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.gameObject.scene != gameObject.scene || !light.enabled || light.name == FireAlarmLight) continue;
                if (light.type == LightType.Directional || light.name.StartsWith("Daylight_", System.StringComparison.Ordinal))
                    fillLights.Add((light, light.intensity));
                else
                    lights.Add((light, light.intensity));
            }

            if (lampMaterials == null || lampMaterials.Length == 0) return;
            scratch ??= new MaterialPropertyBlock();
            var materials = new List<Material>();
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != gameObject.scene) continue;
                renderer.GetSharedMaterials(materials);
                for (int slot = 0; slot < materials.Count; slot++)
                {
                    Material material = materials[slot];
                    if (!material || System.Array.IndexOf(lampMaterials, material) < 0) continue;
                    var original = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(original, slot);
                    lamps.Add((renderer, slot, original.HasColor(EmissionColor)
                        ? original.GetColor(EmissionColor)
                        : material.GetColor(EmissionColor), original));
                }
            }
        }

        void PlayFlickerSound()
        {
            if (!flickerClip) return;
            if (!flickerAudio)
            {
                var emitter = new GameObject("Corridor Light Flicker Audio");
                emitter.transform.SetParent(transform);
                flickerAudio = emitter.AddComponent<AudioSource>();
                flickerAudio.playOnAwake = false;
                flickerAudio.spatialBlend = 1f;
                flickerAudio.minDistance = 2f;
                flickerAudio.maxDistance = 18f;
                flickerAudio.dopplerLevel = 0f;
                flickerAudio.rolloffMode = AudioRolloffMode.Linear;
            }
            flickerAudio.transform.position = (corridorStart.position + corridorEnd.position) * 0.5f;
            flickerAudio.PlayOneShot(flickerClip);
        }

        IEnumerator Flicker()
        {
            SetBrightness(0.18f);
            yield return new WaitForSeconds(0.10f);
            SetBrightness(0.78f);
            yield return new WaitForSeconds(0.14f);
            SetBrightness(0.24f);
            yield return new WaitForSeconds(0.10f);
            SetBrightness(0.66f);
            yield return new WaitForSeconds(0.16f);
            SetBrightness(0f);
            DimEnvironment();
            EnableNearSurfaceLight();
            flicker = null;
        }

        void EnableNearSurfaceLight()
        {
            if (!player) return;
            var walker = player.GetComponent<DormitoryWalkthrough>();
            var view = walker && walker.viewCamera ? walker.viewCamera : player.GetComponentInChildren<Camera>();
            if (!view) return;

            if (!nearSurfaceLight)
            {
                var source = new GameObject("Blackout Near Surface Light");
                nearSurfaceLight = source.AddComponent<Light>();
                nearSurfaceLight.type = LightType.Point;
                nearSurfaceLight.range = NearSurfaceLightRange;
                nearSurfaceLight.intensity = NearSurfaceLightIntensity;
                nearSurfaceLight.shadows = LightShadows.None;
            }
            nearSurfaceLight.transform.SetParent(view.transform, false);
            nearSurfaceLight.enabled = true;
        }

        void DimEnvironment()
        {
            foreach (var entry in fillLights)
                if (entry.light)
                    entry.light.intensity = entry.intensity * BlackoutEnvironmentBrightness;

            if (gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) return;
            originalEnvironment = (RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor,
                RenderSettings.ambientGroundColor, RenderSettings.reflectionIntensity);
            environmentDimmed = true;
            RenderSettings.ambientSkyColor = originalEnvironment.sky * BlackoutEnvironmentBrightness;
            RenderSettings.ambientEquatorColor = originalEnvironment.equator * BlackoutEnvironmentBrightness;
            RenderSettings.ambientGroundColor = originalEnvironment.ground * BlackoutEnvironmentBrightness;
            RenderSettings.reflectionIntensity = originalEnvironment.reflections * BlackoutEnvironmentBrightness;
        }

        void SetBrightness(float brightness)
        {
            foreach (var entry in lights)
            {
                if (!entry.light) continue;
                entry.light.intensity = entry.intensity * brightness;
                entry.light.enabled = brightness > 0f;
            }
            foreach (var entry in lamps)
            {
                if (!entry.renderer) continue;
                entry.renderer.GetPropertyBlock(scratch, entry.slot);
                if (scratch.isEmpty) entry.renderer.GetPropertyBlock(scratch);
                scratch.SetColor(EmissionColor, entry.emission * brightness);
                entry.renderer.SetPropertyBlock(scratch, entry.slot);
            }
        }

        internal void ResetFloor()
        {
            armed = false;
            if (flicker != null) StopCoroutine(flicker);
            flicker = null;
            if (flickerAudio) flickerAudio.Stop();
            if (nearSurfaceLight) nearSurfaceLight.enabled = false;

            foreach (var entry in lights)
            {
                if (!entry.light) continue;
                entry.light.intensity = entry.intensity;
                entry.light.enabled = true;
            }
            foreach (var entry in lamps)
                if (entry.renderer)
                    entry.renderer.SetPropertyBlock(entry.original.isEmpty ? null : entry.original, entry.slot);
            foreach (var entry in fillLights)
                if (entry.light)
                    entry.light.intensity = entry.intensity;
            if (environmentDimmed && gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
            {
                RenderSettings.ambientSkyColor = originalEnvironment.sky;
                RenderSettings.ambientEquatorColor = originalEnvironment.equator;
                RenderSettings.ambientGroundColor = originalEnvironment.ground;
                RenderSettings.reflectionIntensity = originalEnvironment.reflections;
            }
            environmentDimmed = false;
            lights.Clear();
            lamps.Clear();
            fillLights.Clear();
        }

        void OnDisable() => ResetFloor();

        void OnDestroy()
        {
            if (nearSurfaceLight) Destroy(nearSurfaceLight.gameObject);
        }
    }
}
