# 🚀 Control de Fichajes Biométricos

Sistema de gestión y control de fichajes biométricos desarrollado con arquitectura limpia en .NET, diseñado para la sincronización segura de huellas dactilares, control de sucursales y registro de asistencia corporativa en tiempo real.

---

## 🛠️ Tecnologías y Stack Principal

* **Plataforma:** .NET (C#) / Windows Forms
* **Arquitectura:** Clean Architecture (Domain, Application/Infrastructure, Presentation)
* **Base de Datos:** SQLite (Local database sync)
* **Comunicación:** API Client REST / Servicios Biométricos dedicados

---

## 📂 Estructura del Proyecto

```text
ControlFichajesBiometricos-main/
│
├── Domain/                 # Entidades de negocio, DTOs e Interfaces principales
│   ├── DTO/                # Objetos de transferencia (Catálogo, Huella, Login, Sucursal)
│   ├── Interfaces/         # Contratos de servicios y repositorios (IBiometricService, etc.)
│   └── Models/             # Modelos de dominio (Empleado, Empresa, Fichada, Huella)
│
├── Infrastructure/         # Capa de datos, persistencia y servicios externos
│   ├── Data/               # Contexto y gestión de base de datos local (SQLite)
│   ├── Repositories/       # Implementación de repositorios de datos
│   └── Services/           # Cliente API, manejo de credenciales y servicio biométrico
│
├── Presentation/           # Interfaz gráfica y formularios de usuario
│   ├── EnrolarHuellaForm   # Módulo de enrolamiento biométrico de huellas
│   ├── FormLogin           # Autenticación y acceso de operadores
│   ├── LectorListenerForm  # Listener en tiempo real del lector de huellas
│   ├── MainTrayContext     # Control de la aplicación en la bandeja del sistema (System Tray)
│   └── SeleccionSucursalF  # Selector dinámico de sucursal activa
│
└── Resources/              # Recursos gráficos, iconos y animaciones del sistema
