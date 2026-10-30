#version 330 core
#extension GL_ARB_explicit_attrib_location: enable
in float shade;
uniform vec4 tint;
#include oit.fsh
#if USEOIT == 0
layout(location = 0) out vec4 outColor;
layout(location = 1) out vec4 outGlow;
#endif
void main() {
    vec4 colour = vec4(tint.rgb * shade, tint.a);
#if USEOIT > 0
    OIT(colour, 0.0);
#else
    outColor = colour;
    outGlow = vec4(0.0);
#endif
}
