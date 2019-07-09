using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xenko.Core.Mathematics;
using Xenko.Input;
using Xenko.Engine;

namespace XenkoByteSized {
    public class OrbScript : SyncScript {

        public BoundingBox BoundingBox;

        public override void Update() {
            var worldPos = Entity.Transform.WorldMatrix.TranslationVector;
            var containmentType = BoundingBox.Contains(ref worldPos);
            if (containmentType == ContainmentType.Disjoint) {
                Entity.Scene = null;
            }
        }
    }
}
