//Render Table With Data JSON
var dataTable;
$(document).ready(function () {
    var url = window.location.search; //lấy url hiện tại của website
    if (url.includes('inprocess')) {
        loadDataTable('inprocess');
    }
    else if (url.includes('completed')) {
        loadDataTable('completed');
    }
    else if (url.includes('pending')) {
        loadDataTable('pending');
    }
    else if (url.includes('approved')) {
        loadDataTable('approved');
    }
    else
    {
        loadDataTable('all');
    }
})

function loadDataTable(status) {
    dataTable = $('#tblData').DataTable({
        "ajax": {
            url: '/admin/order/getall?status=' + status,//GET: .../admin/order/GetAll?status=....  để lấy data. --> Area: Admin, Controller: Order, Action: GetAll --> return JSON
            type: 'GET'
        },
        "columns": [
            //Các cột cần hiển thị (Lưu ý các cột này phải có và phải giống tên trong data JSON. data JSON được lấy từ url: '/admin/product/GetAll')
            { data: 'id', "width" : "5%" },
            { data: 'name', "width": "25%" },
            { data: 'phoneNumber', "width": "20%" },
            { data: 'applicationUser.email', "width": "20%" },
            { data: 'orderStatus', "width": "10%" },
            { data: 'orderTotal', "width": "10%" },
            {
                //hiển thị nút Edit
                data: 'id', //key data: lưu id của từng product
                "render": function (data) { //data: dữ liệu Json từ data: 'id' --> data chỉ lưu id
                    //http default của thẻ a là GET
                    return `<div class="w-75 btn-group" role="group">
                                <a href="/admin/order/details?orderId=${data}" class="btn btn-primary mx-2"><i class="bi bi-pencil-square"></i></a>
                            </div>`
                },
                "width": "10%"
            },
        ]
    });
}
