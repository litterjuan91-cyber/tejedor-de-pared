# Tejedor de Pared — proyecto Unity

Juego de plataformas pixel art para **proyectar sobre una pared real**. El personaje camina, trepa y se cuelga con su red de los cuadros, estantes y ganchos de tu pared. Está basado en el proyecto *spider-projection* y en su video, pero con personajes originales.

Todo se genera por código (sprites, sonidos, cámaras), así que no hay que importar ni configurar assets.

## Requisitos

- Unity 6 (6000.x). El proyecto original usaba 6000.3.
- Funciona con el render Built-in o con URP, y con el Input System nuevo o el Input Manager clásico. El joystick solo funciona con el Input System nuevo.

## Instalación (recomendada)

1. En Unity Hub creá un proyecto nuevo con la plantilla **Universal 2D** o **2D (Built-in)**.
2. Copiá la carpeta `Assets/TejedorDePared` de este zip dentro de la carpeta `Assets` de tu proyecto.
3. Esperá a que Unity compile.
4. En el menú de arriba, elegí **Tejedor → 1. Crear escena del juego**.
5. Elegí **Tejedor → 2. Configurar para proyector**.
6. Dale **Play**.

Si preferís no crear la escena, también funciona dándole Play en cualquier escena, incluso vacía: el juego se crea solo.

**Alternativa:** en Unity Hub usá *Add → Add project from disk* sobre la carpeta `TejedorDePared` del zip. Ya trae el `Packages/manifest.json`, y Unity genera el resto.

## Usarlo con el proyector

1. Hacé un build con *File → Build Profiles → Windows/Mac → Build* y abrilo en la compu del proyector.
   - También podés jugar desde el editor con la ventana *Game* maximizada en el monitor del proyector.
2. Apretá **F1** (o **º**) para calibrar. Con **Q** sacás la cuadrícula y quedan solo los contornos.
3. Arrastrá cada rectángulo sobre su objeto real:
   - **N** agrega un cuadro y **K** un gancho.
   - **O** define si es de una vía o sólido, **T** si acepta la red y **Y** si es invisible.
   - **1–4** ponen enemigos.
4. Si la imagen sale torcida, apretá **P** y arrastrá las 4 esquinas para hacer el corner-pin.
5. Guardá con **S**: hay una cuenta regresiva de 3 segundos y **Esc** la cancela.
6. Apretá **G** para que todas las estructuras sean invisibles. Solo se proyectan el personaje, la red, los enemigos y las fichas.

La disposición se guarda como `pared_layout.json` en la carpeta de datos, que se abre desde *Tejedor → Abrir carpeta…*:

- **X** la exporta como `pared_layout_exportado.json`.
- **I** importa `pared_layout_importar.json` desde esa misma carpeta.

## Controles

| Acción | Teclado / mouse | Joystick |
|---|---|---|
| Moverse | A/D o flechas | Stick izquierdo / cruceta |
| Saltar | Espacio | A (sur) |
| Columpiarse (mantener) | Clic izquierdo, F o J | RT o RB |
| Acortar/alargar la red | W/S | Stick arriba/abajo |
| Apuntar | Mouse | Stick derecho |
| Disparar red | Clic derecho, K o E | X / Y |
| Rodar | C o Shift | B (este) |
| Bajar de un borde | S + Espacio | Abajo + A |
| Trepar pared | Empujar hacia la pared + W/S | Igual |
| Pausa | Esc | Start |
| Reiniciar | R (mantener) | Select (mantener) |
| Calibración | F1 o º | Select + Start (mantener) |
| Apagón | F2 o B | — |
| Estructuras invisibles | F3 o G | — |
| Sonido / ayuda | U / H | — |
| Pantalla completa | F11 | — |

## Estructura del código

| Archivo | Qué hace |
|---|---|
| `Scripts/Game.cs` | Constantes de ajuste, estado, bucle principal y lectura de controles |
| `Scripts/Game.Player.cs` | Física del jugador: correr, saltar, paredes, columpio y puntaje de anclas |
| `Scripts/Game.Enemies.cs` | Perseguidor, bombardero, patrulla, torreta, bombas, disparos y fichas al azar |
| `Scripts/Game.Render.cs` | Cámara del mundo → RenderTexture de 640×360 (pixel art) → malla de salida deformable (corner-pin) |
| `Scripts/Game.Calibration.cs` | Editor de pared, homografía del corner-pin e interfaz con OnGUI/GL |
| `Scripts/PixelArt.cs` | Sprites y cuadros (edificio, ventana nocturna…) generados por código |
| `Scripts/Synth.cs` | Efectos de sonido sintetizados |
| `Scripts/Controls.cs` | Teclado, mouse y joystick (Input System nuevo o clásico) |
| `Scripts/WallLayout.cs` | Datos de la pared y guardado en JSON |
| `Scripts/Bootstrap.cs` | Crea el juego al darle Play |
| `Editor/TejedorMenu.cs` | Menú "Tejedor" del editor |

**Unidades:** toda la lógica trabaja en píxeles de una salida de 1920×1080. Una unidad de Unity equivale a 100 px, y la cámara ortográfica tiene tamaño 5,4. Los valores de ajuste (velocidad, salto, columpio…) son los de `gameplay_spec.json` del proyecto original y están al principio de `Game.cs`.
