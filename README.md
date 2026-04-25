# cse165-project1-group2

Spawning items:
pressing the joystick: spawn item in front of the camera
moving joystick: moving the item around in xy plane
moving the joystick forward/backward while pressing the grip: moving the item forward/backward
rotating the controller while holding the trigger: rotating the item
Press the joystick again: drop the item (enabling gravity and collision)

Selecting item:
Cast a ray by holding the grip/trigger on the left controller
Any medical equipment hit by the ray is highlighted in blue
Releasing the grip/trigger while the ray is still hitting an object makes that object be selected, highlighted in red
The object is pulled to 1.5 units from the left controller. One can move the object by swinging the left controller (like swinging a kebab)
Pressing the trigger on the right controller while rotating the controller changes the orientation of the selected object
Pressing the grip of the right controller while moving the controller up/down scales up/down the object
Pressing the trigger/grip on the left controller again releases the object

Moving/Orientation
Cast a ray by holding the grip/trigger on the left controller
If the ray does not hit any object but hits the ground plane, it teleports the main camera to the collision point (plus the camera offset in the Y-axis)
Pushing the joystick on the right controller left/right rotates the main camera left/right
