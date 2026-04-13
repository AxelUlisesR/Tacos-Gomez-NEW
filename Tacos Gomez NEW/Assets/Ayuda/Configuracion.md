# ⚙️ Configuración y Mantenimiento
Este módulo es de **uso exclusivo para el Administrador** y permite proteger la integridad de la información.

## Copias de Seguridad (Backup)
Es vital realizar respaldos periódicos para evitar pérdida de datos por fallos de hardware.
1. Haz clic en **Crear Respaldo Ahora**.
2. Se abrirá una ventana de Windows; selecciona la carpeta de destino (se recomienda una memoria USB o la Nube).
3. El sistema generará un archivo `.sql` con toda la estructura y datos actuales.

## Restauración de Datos
**⚠️ Atención:** Este proceso borrará la base de datos actual y la reemplazará con la del archivo seleccionado.
1. Haz clic en **Restaurar desde Archivo**.
2. Selecciona un archivo de respaldo generado anteriormente.
3. El sistema utilizará las herramientas de **PostgreSQL 17** para reconstruir la base de datos.
4. Al finalizar, es necesario **reiniciar la aplicación** para cargar los datos restaurados.

## Requisitos Técnicos
Para que estas funciones operen, el equipo debe tener instalado:
- PostgreSQL 17 (Ruta: `C:\Program Files\PostgreSQL\17\bin`)
- Acceso de escritura en la carpeta de destino del respaldo.