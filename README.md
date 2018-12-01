# ReflectorProbe
Way to customize how reflection probes are baked in Unity

In short, you place a Reflector Probe in your scene instead of Reflection Probe. 
It wraps on top of Unity's probes and offer all the same features.

Except it also offer you a property to hook a camera of your choice.
This allows you to plug in a camera with post effect on it, to give your reflection special looks.
For example, a reflection with bloom in it gives a nice effect on brushed metal where environment should look sharp, but light source should be blurry.

Another advantage of using a custom camera is the ability to hook custom sky that another camera is seeing.

Under the hood, it also offer better frame splits when updating.
Unity "frame splitting" is 1 frame per face, while here it goes up to 4 frames per face (some blit done once at a time)
