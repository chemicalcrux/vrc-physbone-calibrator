using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace ChemicalCrux.PhysboneCalibrator.Editor
{
    public class MenuHierarchy
    {
        public string label;
        public Texture2D icon;

        public readonly List<VRCExpressionsMenu.Control> leaves = new();
        private List<MenuHierarchy> branches = new();

        private int Count => leaves.Count + branches.Count;

        public void TrySplit(int slotLimit)
        {
            if (Count <= slotLimit)
                return;

            int slots = slotLimit - branches.Count;

            if (slots > 0)
            {
                int perBranch = Mathf.CeilToInt((float)leaves.Count / slots);

                var sorted = leaves.OrderBy(item => item.name).ToList();
                
                for (int slice = 0; slice < sorted.Count; slice += perBranch)
                {
                    MenuHierarchy branch = new();
                    branch.leaves.AddRange(sorted.Skip(slice).Take(perBranch));
                    branch.label = branch.leaves[0].name + " - " + branch.leaves[^1].name;
                    branches.Add(branch);
                }

                leaves.Clear();
            }

            if (branches.Count > slotLimit)
            {
                List<MenuHierarchy> newBranches = new();

                branches = branches.OrderBy(item => item.label).ToList();

                int perBranch = Mathf.CeilToInt((float)branches.Count / slotLimit);

                for (int slice = 0; slice <= branches.Count; slice += perBranch)
                {
                    MenuHierarchy branch = new();
                    branch.branches.AddRange(branches.Skip(slice).Take(perBranch));
                    branch.label = branch.branches[0].label + " - " + branch.branches[^1].label;
                    newBranches.Add(branch);
                }

                branches.Clear();
                branches.AddRange(newBranches);

                foreach (var branch in newBranches)
                {
                    branch.TrySplit(slotLimit);
                }
            }
        }

        public VRCExpressionsMenu Resolve(GeneratorContext context, string prefix)
        {
            VRCExpressionsMenu menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.name = prefix + " - " + label;
            menu.Parameters = context.generatedParameters;
            
            foreach (var leaf in leaves)
            {
                menu.controls.Add(leaf);
            }

            foreach (var branch in branches)
            {
                menu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = branch.label,
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = branch.Resolve(context, prefix + " - " + label),
                    icon = branch.FindIcon()
                });
            }

            return menu;
        }

        public MenuHierarchy GetOrCreateBranch(string branchLabel)
        {
            foreach (var branch in branches)
            {
                if (branch.label == branchLabel)
                {
                    return branch;
                }
            }

            return CreateBranch(branchLabel);
        }

        public MenuHierarchy CreateBranch(string branchLabel)
        {
            var newBranch = new MenuHierarchy
            {
                label = branchLabel
            };

            branches.Add(newBranch);

            return newBranch;
        }

        public Texture2D FindIcon()
        {
            if (icon)
                return icon;

            foreach (var branch in branches)
            {
                var found = branch.FindIcon();

                if (found)
                    return found;
            }

            foreach (var leaf in leaves)
            {
                if (leaf.icon)
                    return leaf.icon;
            }

            return null;
        }
    }
}