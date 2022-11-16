# Coocoo3D
![image](https://user-images.githubusercontent.com/63526047/150717738-58eb5cfe-dc19-417d-b389-f8f35607a679.png)

An MMD renderer with extremely low CPU requirements, supports DirectX12 and DXR real-time ray tracing, and has a programmable rendering pipeline.

(Ancient version) video[https://www.bilibili.com/video/BV1p54y127ig/](https://www.bilibili.com/video/BV1p54y127ig/)

## basic skills
* Load pmx, glTF model
* load vmd action
* Play animation
* Record image sequence

## Graphics functions
* Programmable rendering pipeline
* Decals
* Baking Skybox
* Post-processing
* Ray Traced Reflections
* Screen space reflection (using Hierarchical ZBuffer)
* Global illumination
* SSAO
*TAA
* AMD Radeon Prorender rendering (self-illumination not supported)

## screenshot

Global Illumination: Off
![Screenshot 2022-03-14 213422](https://user-images.githubusercontent.com/63526047/158182829-b817ec09-e5fa-4f30-9753-3fd5f0d1a6bc.png)

Global Illumination: On
![Screenshot 2022-03-14 213438](https://user-images.githubusercontent.com/63526047/158182978-0b84d0bf-99cd-489d-8522-6684d9cf48d7.png)

Volumetric Light: On
![Screenshot 2022-03-14 213644](https://user-images.githubusercontent.com/63526047/158183360-0465767c-e416-4d1b-b342-56b2b14dcc4e.png)

Ray Tracing: On Specular Reflection
![Screenshot 2022-03-14 213859](https://user-images.githubusercontent.com/63526047/158183752-837d9481-96b8-4097-ae7a-1c15477a217e.png)

![Screenshot 2022-03-17 131925](https://user-images.githubusercontent.com/63526047/158742418-dca992c7-bc91-4bdb-8569-0a541887cd5e.png)

Decal support
![Screenshot 2022-05-26 201224](https://user-images.githubusercontent.com/63526047/170485548-466c2199-ccdb-41fc-9c2f-a1930d77ce73.png)

## Using Radeon Prorender
RadeonProRender64.dll and Northstar64.dll are not included in this repository, get them from the Radeon Prorender SDK.

[https://gpuopen.com/radeon-pro-render/](https://gpuopen.com/radeon-pro-render/)

When using Radeon Prorender, the software path and image path must be in English.