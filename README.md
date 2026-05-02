# X20Guard 🛡️

**X20Guard** es una utilidad de sistema residente (en segundo plano) desarrollada en C# y diseñada para mantener configuraciones personalizadas de pantalla (overclock de hercios) en equipos donde el controlador de gráficos tiende a restablecerse a los valores predeterminados tras eventos de energía o suspensión.

Esta herramienta fue creada específicamente para solucionar el terco problema del "perfil fantasma a 60Hz", donde los controladores integrados (como **Intel HD Graphics**) se niegan a realizar un *Handshake* completo con monitores modificados (ej. EDID de 100Hz) al despertar de la suspensión o al usar configuraciones de portátiles con tapas cerradas.

## 🚀 Características Principales

* **Recuperación a Nivel de Hardware (Nuevo motor):** Atrás quedaron los atajos de teclado inestables. X20Guard utiliza WMI y la herramienta de núcleo `pnputil` de Windows para realizar un reinicio silencioso y nativo del controlador gráfico (WDDM), forzando la lectura del EDID real del monitor en milisegundos.
* **Sistema Inteligente Anti-Rebote:** El programa está blindado contra las "tormentas de eventos" de Windows. Sobrevive a desconexiones físicas de cables, bajones de batería y cambios de topología sin crashear gracias a su sistema de retrasos asíncronos y bloqueo temporal.
* **Monitoreo en Tiempo Real:** Rastrea la resolución activa y la frecuencia de actualización mediante la API `EnumDisplaySettings` de `User32.dll`.
* **Feedback Visual Silencioso:** Icono dinámico en la bandeja del sistema (System Tray) con código de colores:
    * 🟢 **Verde:** Configuración correcta y enganchada (ej. 1440x900 @ 100Hz).
    * 🔴 **Rojo:** Alerta detectada (Hercios por debajo del objetivo, preparando el reinicio de GPU).
    * ⚪ **Gris:** Modo de espera o resolución distinta (ej. pantalla interna de laptop activa).
* **Eficiencia Térmica y de Recursos:** Proceso configurado con clase de prioridad Alta, consumiendo recursos de CPU y RAM casi nulos.
* **Caja Negra (Logging Automático):** Sistema de registro de errores fatales en el Escritorio para facilitar el debugging en caso de fallos del sistema operativo.

## 🛠️ Requisitos

* **Sistema Operativo:** Windows 10 / 11 (64-bit).
* **Hardware:** Monitor con overclock configurado previamente (ej. mediante CRU - Custom Resolution Utility) y una GPU (como Intel HD Graphics).
* **Framework:** Compilado para .NET 8 (Configurado como Single-File Executable).

## 📦 Instalación y Configuración (Autonomía Total)

1.  **Descarga:** Descarga el archivo `.exe` desde la pestaña de *Releases* (Empaquetado Single-File, no requiere instalar .NET).
2.  **Privilegios (Obligatorio):** Asegúrate de configurar las propiedades del ejecutable para que siempre inicie como **Administrador** (necesario para que `pnputil` pueda reiniciar la GPU).
3.  **Persistencia Perfecta (Recomendado):** Configura una tarea en el **Programador de Tareas de Windows**:
    * **Desencadenador:** Al iniciar sesión (Cualquier usuario).
    * **Configuración:** Ejecutar con los privilegios más altos.
    * **Condiciones:** *Desmarcar* "Iniciar la tarea solo si el equipo está conectado a la corriente alterna".
    * **Configuración adicional:** *Desmarcar* "Detener la tarea si se ejecuta durante más de 3 días".

## 💻 Uso

Una vez iniciada, la aplicación arranca de forma invisible y se aloja en el área de iconos ocultos junto al reloj. No hay ventanas molestas.
* **Hover (Pasar el ratón):** Muestra un *tooltip* con la resolución y frecuencia detectada en ese instante.
* **Clic derecho -> Reiniciar GPU:** Permite forzar un ciclo de poder manual sobre la tarjeta gráfica (Estilo Restart64) en caso de fallos externos.
* **Clic derecho -> Salir:** Finaliza el monitoreo y cierra la aplicación de forma segura.

## ⚠️ Notas Técnicas de la Arquitectura

X20Guard no actúa por impulsos. La aplicación utiliza un retraso calculado (Delay) tras dispararse el evento `DisplaySettingsChanged`. Esto permite que el hardware de video termine sus comprobaciones internas inestables de 60Hz antes de que la aplicación ejecute el reseteo nativo de la GPU, garantizando que el parche sea efectivo el 100% de las veces sin entrar en bucles de parpadeos.

---
*Desarrollado con ❤️ para la comunidad de entusiastas del hardware y el overclocking de monitores.*