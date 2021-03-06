﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using NitroxModel.Helper;
using NitroxModel.Logger;
using NitroxServer.GameLogic.Spawning;
using NitroxServer.UnityStubs;

namespace NitroxServer.Serialization
{
    /**
     * Parses the files in build18 in the format of batch-cells-x-y-z-slot-type.bin
     * These files contain serialized GameObjects with EntitySlot components. These
     * represent areas that entities (creatures, objects) can spawn within the world.
     * This class consolidates the gameObject, entitySlot, and cellHeader data to
     * create EntitySpawnPoint objects.
     */
    class BatchCellsParser
    {
        public static readonly Int3 MAP_DIMENSIONS = new Int3(26, 19, 26);
        public static readonly Int3 BATCH_DIMENSIONS = new Int3(160, 160, 160);

        private readonly ServerProtobufSerializer serializer;
        private readonly Dictionary<string, Type> surrogateTypes = new Dictionary<string, Type>();

        public BatchCellsParser()
        {
            serializer = new ServerProtobufSerializer();

            surrogateTypes.Add("UnityEngine.Transform", typeof(Transform));
            surrogateTypes.Add("UnityEngine.Vector3", typeof(Vector3));
            surrogateTypes.Add("UnityEngine.Quaternion", typeof(Quaternion));
        }

        public List<EntitySpawnPoint> GetEntitySpawnPoints()
        {
            Log.Info("Loading batch data...");

            List<EntitySpawnPoint> entitySpawnPoints = new List<EntitySpawnPoint>();

            Parallel.ForEach(Enumerable.Range(1, Map.DIMENSIONS_IN_BATCHES.x), x =>
            {
                for (int y = 1; y <= Map.DIMENSIONS_IN_BATCHES.y; y++)
                {
                    for (int z = 1; z <= Map.DIMENSIONS_IN_BATCHES.z; z++)
                    {
                        Int3 batchId = new Int3(x, y, z);

                        List<EntitySpawnPoint> batchSpawnPoints = ParseBatchData(batchId);

                        lock (entitySpawnPoints)
                        {
                            entitySpawnPoints.AddRange(batchSpawnPoints);
                        }
                    }
                }
            });

            Log.Info("Batch data loaded!");

            return entitySpawnPoints;
        }

        public List<EntitySpawnPoint> ParseBatchData(Int3 batchId)
        {
            List<EntitySpawnPoint> spawnPoints = new List<EntitySpawnPoint>();

            ParseFile(batchId, "", "loot-slots", spawnPoints);
            ParseFile(batchId, "", "creature-slots", spawnPoints);
            ParseFile(batchId, @"Generated\", "slots", spawnPoints);  // Very expensive to load
            ParseFile(batchId, "", "loot", spawnPoints);
            ParseFile(batchId, "", "creatures", spawnPoints);
            ParseFile(batchId, "", "other", spawnPoints);

            return spawnPoints;
        }

        public void ParseFile(Int3 batchId, string pathPrefix, string suffix, List<EntitySpawnPoint> spawnPoints)
        {
            // This isn't always gonna work.
            string path = Properties.Settings.Default.BuildDir;
            string fileName = path + pathPrefix + "batch-cells-" + batchId.x + "-" + batchId.y + "-" + batchId.z + "-" + suffix + ".bin";

            if (!File.Exists(fileName))
            {
                return;
            }

            using (Stream stream = File.OpenRead(fileName))
            {
                CellManager.CellsFileHeader cellsFileHeader = serializer.Deserialize<CellManager.CellsFileHeader>(stream);

                for (int cellCounter = 0; cellCounter < cellsFileHeader.numCells; cellCounter++)
                {
                    CellManager.CellHeader cellHeader = serializer.Deserialize<CellManager.CellHeader>(stream);
                    ProtobufSerializer.LoopHeader gameObjectCount = serializer.Deserialize<ProtobufSerializer.LoopHeader>(stream);

                    for (int goCounter = 0; goCounter < gameObjectCount.Count; goCounter++)
                    {
                        GameObject gameObject = DeserializeGameObject(stream);

                        EntitySpawnPoint esp = EntitySpawnPoint.From(batchId, gameObject, cellHeader);
                        spawnPoints.Add(esp);
                    }
                }
            }
        }

        private GameObject DeserializeGameObject(Stream stream)
        {
            ProtobufSerializer.GameObjectData goData = serializer.Deserialize<ProtobufSerializer.GameObjectData>(stream);

            GameObject gameObject = new GameObject(goData);
            DeserializeComponents(stream, gameObject);

            return gameObject;
        }

        private void DeserializeComponents(Stream stream, GameObject gameObject)
        {
            ProtobufSerializer.LoopHeader components = serializer.Deserialize<ProtobufSerializer.LoopHeader>(stream);

            for (int componentCounter = 0; componentCounter < components.Count; componentCounter++)
            {
                ProtobufSerializer.ComponentHeader componentHeader = serializer.Deserialize<ProtobufSerializer.ComponentHeader>(stream);

                Type type = null;

                if (!surrogateTypes.TryGetValue(componentHeader.TypeName, out type))
                {
                    type = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType(componentHeader.TypeName))
                        .FirstOrDefault(t => t != null);
                }

                Validate.NotNull(type, $"No type or surrogate found for {componentHeader.TypeName}!");

                object component = FormatterServices.GetUninitializedObject(type);
                serializer.Deserialize(stream, component, type);

                gameObject.AddComponent(component, type);
            }
        }
    }
}
