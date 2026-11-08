using UnityEngine;

namespace Diwali.Editor
{
    public static partial class DiwaliSceneBuilder
    {
        // Separate material and emitter tuning keeps the established anar appearance unchanged.
        static ParticleSystem CohesiveSmoke(string name, Transform parent, Vector3 position, float rate, int maximum,
            float size, float rise, float life = 3.2f, float opacity = 0.3f)
        {
            var system = Smoke(name, parent, position, rate, maximum, size, rise, life, opacity);
            var material = Fx("Continuous smoke", "Diwali/Smoke", m =>
            {
                m.SetTexture("_MainTex", smokeMaterial.GetTexture("_MainTex"));
                m.SetFloat("_Cohesion", 0.8f);
                m.SetFloat("_Thickness", 2);
                m.SetFloat("_Blur", 0.8f);
            });
            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            var sheet = system.textureSheetAnimation;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 1);
            var scale = system.sizeOverLifetime;
            scale.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.75f, 1, 2));
            var colour = system.colorOverLifetime;
            colour.color = Fade((0, 0.65f), (0.08f, 1), (0.7f, 0.75f), (1, 0));
            var noise = system.noise;
            noise.strength = 0.035f;
            return system;
        }

        static GameObject BuildButterfly()
        {
            var item = NewItem<ButterflyItem>("Butterfly", CrackerId.Butterfly, out var model);
            item.body = Group("Flying butterfly", model, new Vector3(0, 0.025f, 0));
            Part("Charge", item.body, Lathe(new[] { new Vector2(0, 0), new Vector2(0.016f, 0), new Vector2(0.016f, 0.06f), new Vector2(0, 0.06f) }, 20), teal);
            item.wings = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                var wing = Group("Wing hinge " + i, item.body, new Vector3(side * 0.012f, 0.025f, 0));
                var part = Part("Coloured wing", wing, Carton(0.065f, 0.004f, 0.07f), i == 0 ? rose : teal, new Vector3(side * 0.028f, 0, 0));
                part.transform.localRotation = Quaternion.Euler(0, side * 24, 0);
                item.wings[i] = wing;
            }
            item.sparks = SparkEmitter("Wing jets", item.transform, item.body.localPosition, chakriSparks, 500, 0.65f);
            item.smoke = CohesiveSmoke("Butterfly smoke", item.body, Vector3.zero, 14, 60, 0.22f, 0.25f);
            AddFuse(item, new Vector3(0, 0.06f, 0), new Vector3(0, 0.08f, 0.055f));
            return Finish(item, "Butterfly", new Vector3(0, 0.045f, 0), new Vector3(0.18f, 0.09f, 0.12f), 0.18f);
        }

        static GameObject BuildPopper()
        {
            var item = NewItem<PopperItem>("Hand Popper", CrackerId.Popper, out var model);
            var paper = Mat("Popper tissue", new Color(0.93f, 0.87f, 0.72f), 0, 0.1f);
            Part("Paper pouch", model, Lathe(SphereProfile(0.018f, 0.018f), 16), paper);
            Part("Twisted paper", model, Lathe(new[] { new Vector2(0.009f, 0.027f), new Vector2(0.003f, 0.04f), new Vector2(0.008f, 0.058f), new Vector2(0, 0.06f) }, 8), paper);
            item.smoke = CohesiveSmoke("Popper puff", item.transform, Vector3.zero, 0, 16, 0.12f, 0.3f, 1.4f, 0.3f);
            item.restSeconds = 2;
            return Finish(item, "Hand Popper", new Vector3(0, 0.03f, 0), new Vector3(0.055f, 0.07f, 0.055f), 0.06f);
        }
    }
}
