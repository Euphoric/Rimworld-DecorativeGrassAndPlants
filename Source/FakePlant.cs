using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Euphoric.DecorativeGrassAndPlants
{
    public class FakePlantExtension : DefModExtension
    {
        public float visualSize = 1;
        public int maxMeshCount = 1;
        public float topWindExposure = 0.25f;
    }

    [StaticConstructorOnStartup]
    public class FakePlant : ThingWithComps
    {
        private string cachedLabelMouseover;
        private static Color32[] workingColors = new Color32[4];

        [TweakValue("Graphics", -1f, 1f)] private static float LeafSpawnRadius = 0.4f;
        [TweakValue("Graphics", 0.0f, 2f)] private static float LeafSpawnYMin = 0.3f;
        [TweakValue("Graphics", 0.0f, 2f)] private static float LeafSpawnYMax = 1f;

        public override string LabelMouseover
        {
            get
            {
                if (cachedLabelMouseover == null)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.Append((string)def.LabelCap);
                    cachedLabelMouseover = stringBuilder.ToString();
                }

                return cachedLabelMouseover;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (Current.ProgramState != ProgramState.Playing || respawningAfterLoad)
                return;
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Blight firstBlight = Position.GetFirstBlight(Map);
            base.DeSpawn(mode);
            firstBlight?.Notify_PlantDeSpawned();
        }

        public override void Tick()
        {
            base.Tick();
            if (!this.IsHashIntervalTick(2000))
                return;
            TickLong();
        }

        public override void TickLong()
        {
            if (Destroyed)
                return;
            base.TickLong();

            cachedLabelMouseover = (string)null;
            DoDropLeaves();
        }


        private void DoDropLeaves()
        {
            if (!true ||
                !(MoteMaker.MakeStaticMote(Vector3.zero, Map, ThingDefOf.Mote_Leaf) is MoteLeaf moteLeaf))
                return;

            float num1 = def.graphicData.drawSize.x;
            Vector3 vector3 = Rand.InsideUnitCircleVec3 * LeafSpawnRadius;
            Vector3 position = Position.ToVector3Shifted() +
                               Vector3.up * Rand.Range(LeafSpawnYMin, LeafSpawnYMax) + vector3 +
                               Vector3.forward * def.graphicData.shadowData.offset.z;
            double num2 = (double)Rand.Value * (double)2000.TicksToSeconds();
            int num3 = (double)vector3.z > 0.0 ? 1 : 0;
            double num4 = (double)num1;
            moteLeaf.Initialize(position, (float)num2, num3 != 0, (float)num4);
        }

        public override void Print(SectionLayer layer)
        {
            var defPlant = def.GetModExtension<FakePlantExtension>();

            Vector3 vector3 = this.TrueCenter();
            Rand.PushState();
            Rand.Seed = Position.GetHashCode();
            float num3 = def.graphicData.drawSize.x * defPlant.visualSize;
            bool singlePlantWasOfset = false;

            int[] positionIndices = PlantPosIndices.GetPositionIndices(this);
            for (int index = 0; index < positionIndices.Length; index++)
            {
                int num5 = positionIndices[index];
                Vector3 center;
                if (defPlant.maxMeshCount == 1)
                {
                    center = vector3 + Gen.RandomHorizontalVector(0.05f);
                    float z = Position.z;
                    if (center.z - defPlant.visualSize / 2.0 < z)
                    {
                        center.z = z + defPlant.visualSize / 2f;
                        singlePlantWasOfset = true;
                    }
                }
                else
                {
                    int countPerSide = 1;
                    switch (defPlant.maxMeshCount)
                    {
                        case 1:
                            countPerSide = 1;
                            break;
                        case 4:
                            countPerSide = 2;
                            break;
                        case 9:
                            countPerSide = 3;
                            break;
                        case 16:
                            countPerSide = 4;
                            break;
                        case 25:
                            countPerSide = 5;
                            break;
                        default:
                            Log.Error(def + " must have plant.MaxMeshCount that is a perfect square.");
                            break;
                    }

                    float num7 = 1f / countPerSide;
                    center = Position.ToVector3();
                    center.y = def.Altitude;
                    center.x += 0.5f * num7;
                    center.z += 0.5f * num7;
                    center.x += (int)(num5 / countPerSide) * num7;
                    center.z += (num5 % countPerSide) * num7;
                    float max = num7 * 0.3f;
                    center += Gen.RandomHorizontalVector(max);
                }

                bool flipUv = Rand.Bool;
                Material material = Graphic.MatSingle;
                Vector2[] uvs;
                Graphic.TryGetTextureAtlasReplacementInfo(material, def.category.ToAtlasGroup(), flipUv, false,
                    out material, out uvs, out Color32 _);
                SetWindExposureColors(workingColors, this);
                Vector2 size = new Vector2(num3, num3);
                Printer_Plane.PrintPlane(layer, center, size, material, flipUv: flipUv, uvs: uvs,
                    colors: workingColors, topVerticesAltitudeBias: 0.1f,
                    uvzPayload: this.HashOffset() % 1024);
            }


            if (def.graphicData.shadowData != null)
            {
                Vector3 center = vector3 + def.graphicData.shadowData.offset * defPlant.visualSize;
                if (singlePlantWasOfset)
                    center.z = Position.ToVector3Shifted().z + def.graphicData.shadowData.offset.z;
                center.y -= 0.04054054f;
                Vector3 volume = def.graphicData.shadowData.volume * defPlant.visualSize;
                Printer_Shadow.PrintShadow(layer, center, volume, Rot4.North);
            }

            Rand.PopState();
        }

        public static void SetWindExposureColors(Color32[] colors, FakePlant plant)
        {
            colors[1].a = colors[2].a = GetWindExposure(plant);
            colors[0].a = colors[3].a = (byte)0;
        }

        public static byte GetWindExposure(FakePlant plant)
        {
            var defPlant = plant.def.GetModExtension<FakePlantExtension>();
            return (byte)Mathf.Min((float)byte.MaxValue * defPlant.topWindExposure, (float)byte.MaxValue);
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            string str = InspectStringPartsFromComps();
            if (!str.NullOrEmpty())
                stringBuilder.Append(str);
            return stringBuilder.ToString().TrimEndNewlines();
        }


        public static class PlantPosIndices
        {
            private static int[][][] rootList = new int[25][][];
            private const int ListCount = 8;

            static PlantPosIndices()
            {
                for (int index1 = 0; index1 < 25; ++index1)
                {
                    rootList[index1] = new int[8][];
                    for (int index2 = 0; index2 < 8; ++index2)
                    {
                        int[] numArray = new int[index1 + 1];
                        for (int index3 = 0; index3 < index1; ++index3)
                            numArray[index3] = index3;
                        ((IList<int>)numArray).Shuffle<int>();
                        rootList[index1][index2] = numArray;
                    }
                }
            }

            public static int[] GetPositionIndices(FakePlant p)
            {
                var defPlant = p.def.GetModExtension<FakePlantExtension>();
                int maxMeshCount = defPlant.maxMeshCount;
                int index = (p.thingIDNumber ^ 42348528) % 8;
                return rootList[maxMeshCount - 1][index];
            }
        }
    }
}