# 🌮 Registro de Ventas
Bienvenido al módulo operativo principal de **Tacos Gómez**. Aquí podrás gestionar las órdenes de los clientes de manera eficiente.

## Cómo registrar una orden:
1. **Iniciar orden:** Haz clic en el botón **Nuevo Pedido**. El sistema asignará automáticamente el siguiente folio disponible.
2. **Seleccionar Cliente:** Utiliza el buscador inteligente (AutoSuggestBox). Solo empieza a escribir y el sistema te sugerirá clientes registrados.
3. **Seleccionar Vendedor:** Elige al empleado que está atendiendo la mesa en el menú desplegable.
4. **Agregar Productos:** - Selecciona el producto.
   - Indica la cantidad.
   - Haz clic en **Agregar al Plato**.

## 🍽️ Lógica de Platos
Para garantizar un servicio ordenado, el sistema aplica las siguientes reglas automáticas:
- Se permite un máximo de **8 tacos por plato**.
- Si una orden supera esta cantidad, el sistema dividirá automáticamente los productos en "Plato 1", "Plato 2", etc.
- Puedes visualizar el número de plato actual en la parte inferior derecha.

## Finalizar Venta
Una vez completada la orden, haz clic en **Imprimir y Cobrar**. Esto realizará tres acciones:
1. Guardará la venta en la base de datos.
2. Generará un archivo **PDF** con el ticket detallado.
3. Limpiará el formulario para la siguiente orden.