#version 330 core
#extension GL_ARB_explicit_attrib_location: enable
layout(location = 0) in vec3 vertexPositionIn;
layout(location = 2) in vec4 colorIn;
uniform mat4 projectionMatrix;
uniform mat4 viewMatrix;
uniform mat4 modelMatrix;
out float shade;
void main() {
    shade = colorIn.r;
    gl_Position = projectionMatrix * viewMatrix * modelMatrix * vec4(vertexPositionIn, 1.0);
}
