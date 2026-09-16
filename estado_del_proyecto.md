# Estado del Proyecto: GoalLineBackend 🚀

Este documento presenta el estado actual, las capacidades y la arquitectura del backend de **GoalLine**, desarrollado con **.NET 8 (C#)**, así como el progreso en la infraestructura de despliegue en la nube.

---

## 📌 Resumen General del Proyecto

El backend de **GoalLine** está diseñado para recibir, registrar en base de datos y transmitir en tiempo real eventos de disparos a portería (especialmente alertas de goles) originados desde un cliente (por ejemplo, una simulación en Unity).

El proyecto destaca por su alto rendimiento, el uso de comunicación bidireccional en tiempo real y una arquitectura nativa de contenedores optimizada para entornos en la nube con recursos limitados.

---

## 🛠️ Capacidades del Backend

El sistema cuenta con las siguientes capacidades clave implementadas y funcionales:

### 1. Registro y Persistencia NoSQL (MongoDB)

* **Integración**: Conexión nativa con **MongoDB Atlas** mediante el driver oficial de MongoDB para C#.
* **Asincronismo total**: Registro no bloqueante (`InsertOneAsync`) para asegurar baja latencia en cada petición.
* **Modelo de Datos (`Disparo.cs`)**:
  * `Id`: Identificador único autogenerado por MongoDB (`ObjectId`).
  * `CoordenadaX` y `CoordenadaY`: Ubicación exacta del impacto.
  * `Velocidad`: Velocidad registrada del disparo.
  * `FueGol`: Indicador booleano que determina si el disparo ingresó a portería.
  * `TimestampUnity`: Marca de tiempo de origen del simulador.
  * `TimestampServidor`: Marca de tiempo en formato **UTC** inyectada en el servidor al recibir el impacto.

### 2. Comunicación en Tiempo Real (SignalR / WebSockets)

* **Hub de Tiempo Real (`GoalHub`)**: Endpoint configurado en `/goalHub`.
* **Notificaciones Push**: Cuando un disparo resulta en gol (`disparo.FueGol == true`), el servidor emite instantáneamente el evento `"AlertaGol"` a todos los clientes web o simuladores conectados, permitiendo una experiencia interactiva inmediata.

### 3. API REST y Documentación Interactiva

* **Endpoints**:
  * `POST /api/disparos`: Endpoint principal para el envío de información sobre disparos desde dispositivos externos.
* **Swagger/OpenAPI**: Integración completa para explorar, documentar y testear los endpoints directamente desde el navegador en entorno de desarrollo.

### 4. Contenerización Premium (Docker con Optimización Alpine)

* **Multi-Stage Build**: Estructura optimizada utilizando imágenes ligeras de **Alpine Linux** (`8.0-alpine`) tanto para la compilación como para la ejecución.
* **Optimización de Memoria (Bajo Consumo)**:
  Configuraciones específicas en el runtime de .NET optimizadas para servidores con RAM ajustada (por ejemplo, instancias **t2.micro** de AWS de 1GB en la capa gratuita):
  * `DOTNET_gcServer=0`: Desactiva el Garbage Collector de servidor a favor del modo workstation para reducir drásticamente el consumo de memoria basal.
  * `DOTNET_TieredPGO=0`: Desactiva optimizaciones dinámicas pesadas para conservar ciclos de CPU y RAM en el arranque.

---

## 📂 Estructura del Código Fuente

```text
GoalLineBackend/
├── Controllers/
│   └── DisparosController.cs    # Controlador REST para registrar disparos y gatillar alertas
├── Hubs/
│   └── GoalHub.cs               # Hub de SignalR para la comunicación WebSocket en tiempo real
├── Models/
│   ├── Disparo.cs               # Entidad y mapeo de datos de los disparos
│   └── MongoDBSettings.cs       # Estructura de configuración de MongoDB
├── Properties/
│   └── launchSettings.json      # Perfiles de ejecución local
├── Program.cs                   # Punto de entrada de la aplicación y configuración de servicios
├── Dockerfile                   # Configuración del contenedor Docker optimizado para Alpine
├── GoalLineBackend.csproj       # Archivo de definición del proyecto .NET 8
├── appsettings.json             # Configuración general y de base de datos
└── appsettings.Development.json # Configuración específica para el entorno de desarrollo
```

### Enlaces rápidos a archivos clave

* **Configuración del Sistema**: [Program.cs](file:///c:/Users/aacar/Documents/PTR_Backend/GoalLineBackend/Program.cs) | [appsettings.json](file:///c:/Users/aacar/Documents/PTR_Backend/GoalLineBackend/appsettings.json)
* **Controladores y Hubs**: [DisparosController.cs](file:///c:/Users/aacar/Documents/PTR_Backend/GoalLineBackend/Controllers/DisparosController.cs) | [GoalHub.cs](file:///c:/Users/aacar/Documents/PTR_Backend/GoalLineBackend/Hubs/GoalHub.cs)
* **Modelos**: [Disparo.cs](file:///c:/Users/aacar/Documents/PTR_Backend/GoalLineBackend/Models/Disparo.cs) | [MongoDBSettings.cs](file:///c:/Users/aacar/Documents/PTR_Backend/GoalLineBackend/Models/MongoDBSettings.cs)
* **Despliegue**: [Dockerfile](file:///c:/Users/aacar/Documents/PTR_Backend/GoalLineBackend/Dockerfile) | [GoalLineBackend.csproj](file:///c:/Users/aacar/Documents/PTR_Backend/GoalLineBackend/GoalLineBackend.csproj)

---

## 📈 Etapa del Proyecto y Próximos Pasos

### 💻 Etapa 1: Backend Funcional (Completado)

* **Estado**: El código de la aplicación está finalizado, testeado localmente y listo para producción.
* **Base de Datos**: Conectada a un clúster activo en la nube de MongoDB Atlas (`cluster-unaj-ptr`).
* **Dockerización**: Dockerfile completado y probado para su despliegue en formato de contenedor.

### ☁️ Etapa 2: Infraestructura y Despliegue en AWS (En Proceso)

Estamos listos para implementar la infraestructura como código (IaC) para alojar esta solución en la nube de AWS de manera automatizada.

**Hitos Alcanzados en Infraestructura:**

1. **Usuario de Despliegue Creado**: Se ha configurado el usuario IAM `terraform-deploy` en AWS.
2. **Asignación de Grupo**: El usuario está asociado al grupo `TerraGroup`, el cual cuenta con los permisos necesarios para la creación de recursos.
3. **Credenciales Locales**: Las Access Keys del usuario de despliegue han sido generadas y configuradas correctamente en el entorno de desarrollo local (máquina física con **Windows 11**).
4. **Compatibilidad**: Listo para que Terraform interactúe de forma transparente y segura con la API de AWS.

**Próximos Objetivos de Infraestructura (Propuesta):**

* Diseñar los scripts de **Terraform** (`main.tf`, `variables.tf`, etc.) para provisionar:
  * Una red virtual **VPC** con subredes públicas y privadas.
  * Un servicio de contenedores, ya sea mediante **AWS ECS (Fargate)** para un despliegue Serverless de contenedores, o una instancia **AWS EC2 (t2.micro / t3.micro)** con Docker preconfigurado.
  * Configuración de un **Application Load Balancer (ALB)** (si es necesario) o reglas de Security Group para exponer los puertos `8080` (HTTP) y permitir conexiones WebSockets estables.
