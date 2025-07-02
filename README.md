# Sreynit-BlurProject

This project contains two scotoma simulations: Central and Blur. The environment simulates a visual acuity test using the LogMAR charts (size: 62 cm X 65 cm) placed 4m away from the participants' eyes.
1. The blur intensity and radius can be controlled using sliders located in XR Rig > Main Camera > LocalizedBlurEffect.
2. The algorithm for the blur is located in the script Assets/Sreynit-ScotomaCode/GaussianBlurShader.shader.
3. If the Device is not Varjo Aero, please replace EyeTracking.cs with the corresponding script that enables eyetracking for your specific device and pass the eye coordinates in a function called GetNormalizedGazePosition. 
