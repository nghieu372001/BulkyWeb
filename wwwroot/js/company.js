//Render Table With Data JSON
var dataTable;
$(document).ready(function () {
    loadDataTable();
})

function loadDataTable() {
    dataTable = $('#tblData').DataTable({
        "ajax": { url: '/admin/company/getall' },//GET: .../admin/product/getll để lấy data --> Area: Admin, Controller: Company, Action: GetAll
        "columns": [
            //Các cột cần hiển thị (Lưu ý các cột này phải có và phải giống tên trong data JSON. data JSON được lấy từ url: '/admin/product/GetAll')
            { data: 'name', "width" : "15%" },
            { data: 'address', "width": "15%" },
            { data: 'city', "width": "15%" },
            { data: 'state', "width": "15%" },
            { data: 'phoneNumber', "width": "15%" },
            {
                //hiển thị nút Edit
                data: 'id', //key data: lưu id của từng product
                "render": function (data) { //data: dữ liệu Json từ data: 'id' --> data chỉ lưu id
                    //http default của thẻ a là GET
                    return `<div class="w-75 btn-group" role="group">
                                <a href="/admin/company/upsert?id=${data}" class="btn btn-primary mx-2"><i class="bi bi-pencil-square"></i> Edit</a>
                                <a onClick="Delete('/admin/company/delete?id=${data}')" class="btn btn-danger mx-2"><i class="bi bi-trash-fill"></i> Delete</a>
                            </div>`
                },
                "width": "25%"
            },
        ]
    });
}


//Message Popup When Delete
function Delete(url) {
    Swal.fire({
        title: "Are you sure?",
        text: "You won't be able to revert this!",
        icon: "warning",
        showCancelButton: true,
        confirmButtonColor: "#3085d6",
        cancelButtonColor: "#d33",
        confirmButtonText: "Yes, delete it!"
    }).then((result) => {
        if (result.isConfirmed) {
            //CALL API To DELETE
            $.ajax({
                url: url,
                type: 'DELETE', // --> DELETE: ../admin/comapy/delete/1
                success: function (data) {//call API DELETE thành công thì sẽ return json và lưu vào biến data
                    dataTable.ajax.reload(); //reload lại table
                    //biến toastr trong thư viện toastr được import trong file _Notification.cshtml
                    toastr.success(data.message)
                }
            })
        }
    });
}