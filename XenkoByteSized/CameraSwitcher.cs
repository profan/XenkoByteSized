using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xenko.Core.Mathematics;
using Xenko.Input;
using Xenko.Engine;
using Xenko.Core.Collections;

namespace XenkoByteSized {
    public class CameraSwitcher : SyncScript {
        
        private int activeCamera;
        public FastList<BasicCameraController> cameras = new FastList<BasicCameraController>();

        public override void Start() {
            activeCamera = cameras.FindIndex((c) => c.Enabled);
        }

        public override void Update() {

            DebugText.Print($"Active Camera: {activeCamera} (TAB to switch)", new Int2(24, 24));

            if (Input.IsKeyPressed(Keys.Tab)) {
                cameras[activeCamera].Enabled = false;
                var nextCameraIndex = (activeCamera + 1) % cameras.Count;
                cameras[nextCameraIndex].Enabled = true;
                activeCamera = nextCameraIndex;
            }

        }
    }
}
