using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using NBTExplorer.Model;
using Substrate.Nbt;

namespace DrunkenSquirrel.SquirrelCraft.NBTConverter
{
    class Program
    {
        private static Dictionary<int, int> s_itemIdDictionary;

        private static int cnt = 0;

        private static int cobbled = 0;

        private static List<DataNode> s_taggedForDeletion = new List<DataNode>();

        static void Main(string[] args)
        {

            if (args.Length != 3)
            {
                Console.WriteLine("Usage: <PathToOldWorldDat> <PathToNewWorldDat> <PathToPlayerDat>");
                return;
            }

            var oldPath = args[0];
            var newPath = args[1];
            Console.WriteLine($"Importing old Items from {oldPath}");
            var oldItems = GetItemDataList(new NbtPathEnumerator(oldPath).FirstOrDefault());
            Console.WriteLine($"Importing new Items from {newPath}");
            var newItems = GetItemDataList(new NbtPathEnumerator(newPath).FirstOrDefault());

            s_itemIdDictionary = new Dictionary<int, int>();

            foreach (var oldItem in oldItems)
            {
                var oldId = oldItem.Value;

                try
                {
                    var newId = newItems[oldItem.Key];
                    if (!s_itemIdDictionary.ContainsKey(oldId))
                    {
                        s_itemIdDictionary.Add(oldId, newId);
                    }
                    else
                    {
                       // Console.WriteLine($"Double key found tried to add: {oldItem}. ExistingId: , {s_itemIdDictionary[oldId]}");
                    }                    
                }
                catch (KeyNotFoundException ex)
                {
                    Console.WriteLine($"Could not match value for itemId: {oldItem.Value}, Name: {oldItem.Key}");
                }                
            }

            var playerDat = args[2];
            var playerDatNew = playerDat + ".dat";
            File.Copy(playerDat, playerDatNew, true);
            
            foreach (var foobar in new NbtPathEnumerator(playerDatNew))
            {
                var inventoryRoot = GetInventoryRoot(foobar);

                UpdateSubTree(inventoryRoot);
                Console.WriteLine($"Deleting {s_taggedForDeletion.Count} Slots");
                inventoryRoot.Root.Save();
            }

            Console.WriteLine($"Changed {cnt} items, cobbled {cobbled} items");
            Console.Read();
        }

        private static void UpdateSubTree(DataNode subRoot)
        {
            subRoot.Expand();
            var idTag = subRoot.Nodes.FirstOrDefault(x => x?.NodeName?.ToUpper().Equals("ID") ?? false);

            if (idTag != null)
            {
                var shortNode = (idTag as TagShortDataNode);
                var shortTag = (shortNode?.Tag as TagNodeShort);

                if (shortTag != null)
                {
                    var oldValue = shortTag.Data;
                    if (s_itemIdDictionary.ContainsKey(oldValue))
                    {
                        shortNode.Parse(s_itemIdDictionary[oldValue].ToString());
                        ++cnt;                        
                    }
                    else
                    {
                        shortNode.Parse("4");
                        //s_taggedForDeletion.Add(idTag.Parent);
                        ++cobbled;
                    }

                    
                   // Console.WriteLine($"Changed item: {oldValue} to {shortTag.Data}" );
                    
                }                
            }

            foreach (var node in subRoot.Nodes)
            {
                UpdateSubTree(node);
            }

            var foo = s_itemIdDictionary.First(x => x.Value == 4761);
        }

        
        private static DataNode GetInventoryRoot(DataNode playerRoot)
        {           
            return playerRoot.Nodes.FirstOrDefault(x => x.NodeName.Equals("Inventory") || x.NodeName.Equals("Baubles.Inventory"));           
        }

        /// <summary>
        /// Gets the expanded item data nodes out of a level.dat root
        /// </summary>
        /// <param name="dataRoot"></param>
        /// <returns></returns>
        private static Dictionary<string, int> GetItemDataList(DataNode dataRoot)
        {

            // TODO: If I ever publish it I should think about making this more flexible.
            dataRoot.Expand();
            var fmlNode = dataRoot.Nodes.FirstOrDefault(x => x.NodeName.Contains("FML"));
            if (fmlNode == null)
            {
                Console.WriteLine("Could not find Fuck My Life (FML) Node.");
                return null;
            }
            
            fmlNode.Expand();
            var itemDataNode = fmlNode.Nodes.FirstOrDefault(x => x.NodeName.Contains("ItemData"));

            if (itemDataNode == null)
            {
                Console.WriteLine("Could not find item data Node");
                return null;
            }
            itemDataNode.Expand();
            var result = new Dictionary<string, int>();

            int err = 0;
            int succ = 0;

            foreach (var itemNode in itemDataNode.Nodes)
            {
                var compItemNode = itemNode as TagCompoundDataNode;
                if (compItemNode == null)
                {
                    ++err;
                    continue;                    
                }

                compItemNode.Expand();

                TagStringDataNode kNode = compItemNode.Nodes.FirstOrDefault(x => x.NodeName.Equals("K")) as TagStringDataNode;
                TagIntDataNode vNode = compItemNode.Nodes.FirstOrDefault(x => x.NodeName.Equals("V")) as TagIntDataNode;

                if (kNode != null && vNode != null)
                {                                                                        
                    
                    var kValue = (kNode.Tag as TagNodeString)?.Data;
                    var vValue = (vNode.Tag as TagNodeInt)?.Data;

                    if (string.IsNullOrWhiteSpace(kValue) || !vValue.HasValue)
                    {
                        ++err;
                        continue;
                    }

                    ++succ;
                    if (!kValue.ToUpper().Contains("ROCKHOUNDING"))
                    {
                        result.Add(kValue, vValue.Value);                        
                    }
                    else
                    {
                        Console.WriteLine("Fuck Rockhounding once again");
                    }
                    
                }
                else
                {
                    ++err;
                }
            }

            Console.WriteLine($"Successfully imported {succ} tag as items, {err} tags could not be imported");

            return result;
        }
    }
}
