using System;
using System.Drawing;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ImageProcessing
{
    /// <summary>
    /// Form chính của ứng dụng xử lý ảnh
    /// 
    /// Chức năng:
    /// - Button 1: Nhập ảnh từ camera hoặc file
    /// - Button 2: Tiền xử lý ảnh (Gaussian blur, threshold)
    /// - Button 3: Xử lý cuối cùng (Canny, contour detection)
    /// - Button Reset: Xóa tất cả dữ liệu
    /// 
    /// Quy trình xử lý:
    /// 1. Nhập ảnh gốc → Hiển thị ở PictureBox 1
    /// 2. Tiền xử lý → Hiển thị ở PictureBox 2
    /// 3. Xử lý cuối → Hiển thị ở PictureBox 3
    /// </summary>
    public partial class Form1 : Form
    {
        // ====== BIẾN TOÀN CỤC ======
        
        /// <summary>Lưu trữ ảnh gốc từ camera/file</summary>
        private Mat originalImage;

        /// <summary>Lưu trữ ảnh sau khi tiền xử lý</summary>
        private Mat preprocessedImage;

        /// <summary>Lưu trữ ảnh kết quả cuối cùng</summary>
        private Mat finalImage;

        /// <summary>Constructor - Khởi tạo form</summary>
        public Form1()
        {
            InitializeComponent();
        }

        // ====== BUTTON 1: NHẬP ẢNH ======

        /// <summary>
        /// Nút Button 1: Nhập ảnh từ camera hoặc file
        /// 
        /// Quy trình:
        /// 1. Mở dialog chọn file ảnh
        /// 2. Đọc ảnh từ file sử dụng OpenCV
        /// 3. Kiểm tra xem ảnh có hợp lệ không
        /// 4. Resize ảnh để vừa với PictureBox (400x300)
        /// 5. Hiển thị ảnh gốc lên PictureBox thứ 1
        /// 
        /// Các định dạng ảnh được hỗ trợ:
        /// - JPG/JPEG
        /// - PNG
        /// - BMP
        /// - TIFF
        /// </summary>
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                // === BƯỚC 1: Mở dialog chọn file ===
                OpenFileDialog openFileDialog = new OpenFileDialog();
                
                // Cấu hình filter cho dialog
                openFileDialog.Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";
                openFileDialog.Title = "Chọn ảnh để xử lý";

                // Kiểm tra nếu người dùng nhấn OK
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // === BƯỚC 2: Đọc ảnh từ file ===
                    // Cv2.ImRead() trả về Mat object chứa dữ liệu ảnh
                    // ImreadModes.Color: Đọc ảnh dưới dạng màu (BGR)
                    originalImage = Cv2.ImRead(openFileDialog.FileName);

                    // === BƯỚC 3: Kiểm tra xem ảnh có hợp lệ không ===
                    if (originalImage == null || originalImage.Empty())
                    {
                        MessageBox.Show(
                            "Không thể đọc ảnh. Vui lòng chọn file ảnh hợp lệ!", 
                            "Lỗi", 
                            MessageBoxButtons.OK, 
                            MessageBoxIcon.Error
                        );
                        return;
                    }

                    // === BƯỚC 4: Resize ảnh để vừa với PictureBox ===
                    // Hàm ResizeImage() sẽ tự động giữ tỷ lệ khung hình
                    Mat resizedImage = ResizeImage(originalImage, 400, 300);

                    // === BƯỚC 5: Hiển thị ảnh gốc ===
                    // BitmapConverter.ToBitmap(): Chuyển từ Mat (OpenCV) sang Bitmap (C# GDI+)
                    pictureBox1.Image = BitmapConverter.ToBitmap(resizedImage);
                    
                    // Cập nhật nhãn để cho biết ảnh đã được tải
                    label1.Text = "✓ Ảnh gốc đã được tải";

                    // === BƯỚC 6: Giải phóng bộ nhớ ===
                    // Rất quan trọng! Mat objects dùng unmanaged memory
                    // Cần gọi Dispose() sau khi dùng xong để tránh memory leak
                    resizedImage.Dispose();

                    // Thông báo thành công
                    MessageBox.Show("Ảnh đã được tải thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                // Bắt lỗi ngoại lệ và hiển thị tin nhắn lỗi
                MessageBox.Show(
                    $"Lỗi khi tải ảnh: {ex.Message}", 
                    "Lỗi", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Error
                );
            }
        }

        // ====== BUTTON 2: TIỀN XỬ LÝ ẢNH ======

        /// <summary>
        /// Nút Button 2: Tiền xử lý ảnh
        /// 
        /// Mục đích: Chuẩn bị ảnh cho giai đoạn phát hiện đối tượng
        /// 
        /// Các bước xử lý:
        /// 1. Chuyển đổi từ ảnh màu (BGR) sang ảnh xám (Grayscale)
        ///    - Giảm chiều sâu ảnh từ 3 channels → 1 channel
        ///    - Tập trung vào thông tin độ sáng/tối
        /// 
        /// 2. Áp dụng Gaussian Blur
        ///    - Loại bỏ nhiễu
        ///    - Làm mịn ảnh
        ///    - Giúp thuật toán nhận dạng tốt hơn
        /// 
        /// 3. Áp dụng Adaptive Threshold
        ///    - Chia ảnh thành các vùng nhỏ
        ///    - Tính ngưỡng riêng cho từng vùng
        ///    - Tốt hơn khi có thay đổi ánh sáng không đều
        ///    - Kết quả: ảnh đen-trắng rõ ràng (Binary Image)
        /// 
        /// Kết quả: Ảnh sạch, rõ ràng, sẵn sàng cho Canny detection
        /// </summary>
        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                // === KIỂM TRA DỮ LIỆU ===
                // Đảm bảo rằng người dùng đã tải ảnh từ bước 1
                if (originalImage == null || originalImage.Empty())
                {
                    MessageBox.Show(
                        "Vui lòng tải ảnh trước tiên (Button 1)", 
                        "Cảnh báo", 
                        MessageBoxButtons.OK, 
                        MessageBoxIcon.Warning
                    );
                    return;
                }

                // === BƯỚC 1: CHUYỂN ĐỔI SANG GRAYSCALE ===
                // Grayscale: Ảnh xám chỉ có 1 channel (độ sáng từ 0-255)
                // BGR: Ảnh màu có 3 channels (Blue, Green, Red)
                
                // Tạo Mat mới để lưu ảnh xám
                Mat grayImage = new Mat();
                
                // ColorConversionCodes.BGR2GRAY: Chuyển từ BGR sang Grayscale
                Cv2.CvtColor(originalImage, grayImage, ColorConversionCodes.BGR2GRAY);
                
                // Gán vào biến toàn cục để dùng ở bước tiếp theo
                preprocessedImage = grayImage;

                // === BƯỚC 2: ÁP DỤNG GAUSSIAN BLUR ===
                // Tác dụng: Loại bỏ nhiễu, làm mịn ảnh
                // 
                // Công thức:
                // Output[x,y] = Trung bình trọng số của các pixel xung quanh
                //
                // Thông số:
                // - Kernel size (5, 5): Ma trận 5x5 pixel được xét
                //   (phải là số lẻ: 3, 5, 7, 9...)
                // - Sigma (1.0): Độ lệch chuẩn
                //   Giá trị cao → kết quả mịn hơn
                //   Giá trị thấp → kết quả sắc nét hơn
                
                Mat blurredImage = new Mat();
                Cv2.GaussianBlur(preprocessedImage, blurredImage, new OpenCvSharp.Size(5, 5), 1.0);
                
                preprocessedImage = blurredImage.Clone();
                blurredImage.Dispose();

                // === BƯỚC 3: ÁP DỤNG ADAPTIVE THRESHOLD ===
                // Tác dụng: Chuyển ảnh xám thành ảnh đen-trắng (Binary)
                //
                // Khác với Simple Threshold:
                // - Simple: Dùng 1 giá trị ngưỡng cố định cho toàn bộ ảnh
                //   Output[x,y] = (Input[x,y] > threshold) ? 255 : 0
                //
                // - Adaptive: Tính ngưỡng riêng cho từng vùng
                //   Ngưỡng[x,y] = Trung bình(các pixel xung quanh) - constant
                //   Output[x,y] = (Input[x,y] > Ngưỡng[x,y]) ? 255 : 0
                //
                // Ưu điểm Adaptive:
                // - Xử lý tốt khi ánh sáng không đều
                // - Giữ lại chi tiết tốt hơn
                
                Mat thresholdImage = new Mat();
                Cv2.AdaptiveThreshold(
                    preprocessedImage,           // Ảnh đầu vào
                    thresholdImage,              // Ảnh đầu ra
                    255,                         // Giá trị max (khi pixel > threshold)
                    AdaptiveThresholdTypes.MeanC, // Kiểu: Dùng trung bình các pixel xung quanh
                    ThresholdTypes.Binary,       // Kết quả: Đen hoặc trắng (0 hoặc 255)
                    11,                          // Block size: Kích thước vùng xét (phải là số lẻ)
                    2                            // Constant: Trừ đi từ trung bình
                );
                
                preprocessedImage = thresholdImage.Clone();
                thresholdImage.Dispose();

                // === HIỂN THỊ KẾT QUẢ ===
                // Resize ảnh để vừa với PictureBox
                Mat resizedPreprocessed = ResizeImage(preprocessedImage, 400, 300);

                // Chuyển từ Mat sang Bitmap và hiển thị
                pictureBox2.Image = BitmapConverter.ToBitmap(resizedPreprocessed);
                label2.Text = "✓ Ảnh tiền xử lý hoàn tất";

                // Giải phóng bộ nhớ
                resizedPreprocessed.Dispose();

                MessageBox.Show(
                    "Tiền xử lý ảnh hoàn tất!\n\n" +
                    "Ảnh đã được:\n" +
                    "1. Chuyển sang Grayscale\n" +
                    "2. Áp dụng Gaussian Blur\n" +
                    "3. Áp dụng Adaptive Threshold",
                    "Thông báo", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Lỗi khi tiền xử lý ảnh: {ex.Message}", 
                    "Lỗi", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Error
                );
            }
        }

        // ====== BUTTON 3: XỬ LÝ CUỐI CÙNG ======

        /// <summary>
        /// Nút Button 3: Xử lý ảnh cuối cùng
        /// 
        /// Mục đích: Phát hiện và vẽ các đối tượng (contours)
        /// 
        /// Các bước xử lý:
        /// 
        /// 1. CANNY EDGE DETECTION
        ///    - Phát hiện tất cả các cạnh trong ảnh
        ///    - Cạnh = nơi có sự thay đổi lớn về độ sáng
        ///    - Thông số:
        ///      * Threshold 1 (50): Giá trị dưới đây → không phải cạnh
        ///      * Threshold 2 (150): Giá trị trên đây → chắc chắn là cạnh
        ///      * Cạnh ở giữa → xem có kết nối với cạnh chắc chắn không
        ///      * Aperture size (3): Kích thước kernel Sobel
        ///    - Kết quả: Ảnh nhị phân (đen-trắng) với các cạnh là trắng
        /// 
        /// 2. FIND CONTOURS
        ///    - Tìm tất cả các đường viền (ranh giới)
        ///    - Contour = dãy điểm liên tục tạo thành một hình
        ///    - RetrievalModes.Tree: Lấy tất cả contours với cấu trúc phân cấp
        ///    - ApproxSimple: Nén contours (loại điểm dư thừa)
        /// 
        /// 3. LỌC CONTOURS
        ///    - Chỉ giữ lại các contours có diện tích ≥ 500 pixels²
        ///    - Lý do: Loại bỏ các nhiễu nhỏ, chỉ giữ các đối tượng lớn
        /// 
        /// 4. VẼ KẾT QUẢ
        ///    - Vẽ đường viền (Contour) - màu xanh
        ///    - Vẽ hình chữ nhật bao quanh (Bounding Rectangle) - màu xanh dương
        ///    - Vẽ vòng tròn bao quanh (Min Enclosing Circle) - màu đỏ
        /// 
        /// Kết quả: Ảnh gốc với các hình học phát hiện được vẽ lên
        /// </summary>
        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                // === KIỂM TRA DỮ LIỆU ===
                // Đảm bảo rằng người dùng đã tiền xử lý ảnh từ bước 2
                if (preprocessedImage == null || preprocessedImage.Empty())
                {
                    MessageBox.Show(
                        "Vui lòng tiền xử lý ảnh trước tiên (Button 2)", 
                        "Cảnh báo", 
                        MessageBoxButtons.OK, 
                        MessageBoxIcon.Warning
                    );
                    return;
                }

                // === BƯỚC 1: ÁP DỤNG CANNY EDGE DETECTION ===
                // Mục đích: Phát hiện tất cả các cạnh trong ảnh
                //
                // Quy trình Canny:
                // 1. Tính gradient (độ thay đổi sáng tối) tại mỗi pixel
                //    - Gradient = (độ thay đổi X, độ thay đổi Y)
                //
                // 2. Non-Maximum Suppression: Giữ lại những pixel có gradient cực đại
                //    - Loại bỏ các pixel nằm cạnh những pixel có gradient lớn hơn
                //    - Kết quả: các cạnh mỏng hơn
                //
                // 3. Double Thresholding:
                //    - Pixel có gradient > Threshold2 → Chắc chắn là cạnh (Trắng)
                //    - Pixel có gradient < Threshold1 → Không phải cạnh (Đen)
                //    - Pixel nằm giữa → Tạm thời (xem xét tiếp)
                //
                // 4. Edge Tracking by Hysteresis:
                //    - Các pixel tạm thời kết nối với pixel "chắc chắn là cạnh" → trở thành cạnh
                //    - Các pixel tạm thời không kết nối → Bị xóa
                
                Mat edgeImage = new Mat();
                
                Cv2.Canny(
                    preprocessedImage,  // Ảnh đầu vào (phải là Grayscale hoặc Binary)
                    edgeImage,          // Ảnh đầu ra (các cạnh được phát hiện)
                    50,                 // Threshold 1 (ngưỡng thấp)
                    150,                // Threshold 2 (ngưỡng cao)
                    3                   // Aperture size (kích thước kernel Sobel)
                );

                // === BƯỚC 2: TÌM CONTOURS ===
                // Contour = một dãy điểm liên tục có cùng màu/giá trị
                
                // Tạo bản sao ảnh gốc để vẽ contours
                Mat contourImage = originalImage.Clone();
                
                // Tìm tất cả contours trong ảnh cạnh
                OpenCvSharp.Point[][] contours;      // Mảng các contours
                HierarchyIndex[] hierarchy;           // Cấu trúc phân cấp của contours
                
                Cv2.FindContours(
                    edgeImage,                                      // Ảnh đầu vào (ảnh nhị phân)
                    out contours,                                   // Output: các contours tìm được
                    out hierarchy,                                  // Output: cấu trúc phân cấp
                    RetrievalModes.Tree,                           // Lấy tất cả contours với cấu trúc
                    ContourApproximationModes.ApproxSimple          // Nén contours
                );

                // === BƯỚC 3: LỌC VÀ VẼ CONTOURS ===
                // Thông số lọc
                double minArea = 500;  // Diện tích tối thiểu (pixels²)
                                       // Tính bằng: chiều rộng × chiều cao × π/4 (nếu tròn)
                                       // Hoặc: độ dài × độ rộng (nếu hình chữ nhật)

                // Duyệt qua từng contour
                foreach (var contour in contours)
                {
                    // === Tính diện tích contour ===
                    // Diện tích = số lượng pixels bên trong contour này
                    double area = Cv2.ContourArea(contour);

                    // Nếu diện tích đủ lớn → vẽ contour này
                    if (area > minArea)
                    {
                        // ===== VẼ CONTOUR (Đường viền) =====
                        // Tác dụng: Vẽ đường bao quanh đối tượng
                        // Màu sắc: Xanh (0, 255, 0) trong BGR
                        // Độ dày: 2 pixels
                        Cv2.DrawContours(
                            contourImage,           // Ảnh để vẽ lên
                            new OpenCvSharp.Point[][] { contour },  // Contour cần vẽ
                            -1,                     // Index: -1 = vẽ tất cả
                            new Scalar(0, 255, 0),  // Màu: BGR (Xanh)
                            2                       // Độ dày: 2 pixels
                        );

                        // ===== VẼ BOUNDING RECTANGLE =====
                        // Tác dụng: Vẽ hình chữ nhật tối thiểu bao quanh đối tượng
                        // Hình chữ nhật song song với trục tọa độ
                        var rect = Cv2.BoundingRect(contour);
                        
                        Cv2.Rectangle(
                            contourImage,           // Ảnh để vẽ lên
                            rect,                   // Hình chữ nhật cần vẽ
                            new Scalar(255, 0, 0),  // Màu: BGR (Xanh dương)
                            2                       // Độ dày: 2 pixels
                        );

                        // ===== VẼ MINIMUM ENCLOSING CIRCLE =====
                        // Tác dụng: Vẽ vòng tròn tối thiểu bao quanh đối tượng
                        // Hữu ích để xác định tâm của đối tượng
                        Cv2.MinEnclosingCircle(
                            contour,                // Contour cần xét
                            out OpenCvSharp.Point2f center,  // Output: tâm vòng tròn
                            out float radius        // Output: bán kính vòng tròn
                        );
                        
                        Cv2.Circle(
                            contourImage,           // Ảnh để vẽ lên
                            (OpenCvSharp.Point)center,  // Tâm vòng tròn
                            (int)radius,            // Bán kính
                            new Scalar(0, 0, 255),  // Màu: BGR (Đỏ)
                            2                       // Độ dày: 2 pixels
                        );
                    }
                }

                // === HIỂN THỊ KẾT QUẢ ===
                // Resize ảnh để vừa với PictureBox
                Mat resizedFinal = ResizeImage(contourImage, 400, 300);

                // Chuyển từ Mat sang Bitmap và hiển thị
                pictureBox3.Image = BitmapConverter.ToBitmap(resizedFinal);
                label3.Text = $"✓ Tìm thấy {contours.Length} contours";

                // === GIẢI PHÓNG BỘ NHỚ ===
                edgeImage.Dispose();
                contourImage.Dispose();
                resizedFinal.Dispose();

                // Thông báo kết quả
                MessageBox.Show(
                    $"Xử lý hoàn tất!\n\n" +
                    $"Thông tin:\n" +
                    $"- Tổng số contours: {contours.Length}\n" +
                    $"- Các hình học được vẽ:\n" +
                    $"  * Đường viền (xanh)\n" +
                    $"  * Hình chữ nhật (xanh dương)\n" +
                    $"  * Vòng tròn (đỏ)",
                    "Thông báo", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Lỗi khi xử lý ảnh: {ex.Message}", 
                    "Lỗi", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Error
                );
            }
        }

        // ====== HÀM HỖ TRỢ ======

        /// <summary>
        /// Hàm hỗ trợ: Resize ảnh giữ nguyên tỷ lệ khung hình
        /// 
        /// Mục đích: Ảnh gốc thường quá lớn, cần resize để vừa với PictureBox
        ///          nhưng vẫn giữ nguyên tỷ lệ khung hình
        /// 
        /// Thuật toán:
        /// 1. Tính tỷ lệ scale cho chiều rộng và chiều cao riêng biệt
        /// 2. Chọn tỷ lệ nhỏ nhất (để không vượt quá maxWidth và maxHeight)
        /// 3. Tính kích thước mới dựa trên tỷ lệ đã chọn
        /// 4. Resize ảnh
        /// 
        /// Ví dụ:
        /// - Ảnh gốc: 800x600 pixels
        /// - maxWidth: 400, maxHeight: 300
        /// - Scale width: 400/800 = 0.5
        /// - Scale height: 300/600 = 0.5
        /// - Chọn min(0.5, 0.5) = 0.5
        /// - Kích thước mới: 400x300 pixels (đúng)
        /// </summary>
        /// <param name="image">Ảnh cần resize</param>
        /// <param name="maxWidth">Chiều rộng tối đa (pixels)</param>
        /// <param name="maxHeight">Chiều cao tối đa (pixels)</param>
        /// <returns>Ảnh sau khi resize</returns>
        private Mat ResizeImage(Mat image, int maxWidth, int maxHeight)
        {
            // === KIỂM TRA KÍCH THƯỚC ===
            // Nếu ảnh đã nhỏ hơn maxWidth và maxHeight → không cần resize
            if (image.Width <= maxWidth && image.Height <= maxHeight)
            {
                return image.Clone();
            }

            // === TÍNH TỶ LỆ SCALE ===
            // Scale: Bao nhiêu lần phải resize
            // - Scale width = maxWidth / chiều rộng gốc
            // - Scale height = maxHeight / chiều cao gốc
            // - Chọn tỷ lệ nhỏ nhất để đảm bảo vừa với cả hai chiều
            double scale = Math.Min(
                (double)maxWidth / image.Width, 
                (double)maxHeight / image.Height
            );

            // === TÍNH KÍCH THƯỚC MỚI ===
            int newWidth = (int)(image.Width * scale);
            int newHeight = (int)(image.Height * scale);

            // === RESIZE ẢNH ===
            // Cv2.Resize() thực hiện phép nội suy để tạo ảnh mới
            Mat resized = new Mat();
            Cv2.Resize(
                image,                              // Ảnh đầu vào
                resized,                            // Ảnh đầu ra
                new OpenCvSharp.Size(newWidth, newHeight)  // Kích thước mới
            );

            return resized;
        }

        // ====== BUTTON RESET ======

        /// <summary>
        /// Nút Reset: Xóa tất cả dữ liệu và làm mới giao diện
        /// 
        /// Tác dụng:
        /// 1. Xóa tất cả hình ảnh từ các PictureBox
        /// 2. Reset tất cả nhãn (label)
        /// 3. Giải phóng bộ nhớ của tất cả Mat objects
        /// 4. Làm mới để có thể bắt đầu với ảnh mới
        /// </summary>
        private void buttonReset_Click(object sender, EventArgs e)
        {
            try
            {
                // === XÓA HÌNH ẢNH TỪ PICTUREBOX ===
                // Gọi Dispose() để giải phóng bộ nhớ của Bitmap
                pictureBox1.Image?.Dispose();
                pictureBox2.Image?.Dispose();
                pictureBox3.Image?.Dispose();

                // Gán null để xóa sạch
                pictureBox1.Image = null;
                pictureBox2.Image = null;
                pictureBox3.Image = null;

                // === RESET NHÃN ===
                label1.Text = "Ảnh gốc";
                label2.Text = "Ảnh tiền xử lý";
                label3.Text = "Ảnh kết quả";

                // === GIẢI PHÓNG BỘ NHỚ ===
                // Rất quan trọng! Mat objects dùng unmanaged memory
                // Nếu không gọi Dispose(), sẽ bị memory leak
                originalImage?.Dispose();
                preprocessedImage?.Dispose();
                finalImage?.Dispose();

                // Gán null để chắc chắn
                originalImage = null;
                preprocessedImage = null;
                finalImage = null;

                MessageBox.Show(
                    "Đã reset tất cả dữ liệu!\n" +
                    "Bạn có thể tải ảnh mới bằng cách nhấn Button 1",
                    "Thông báo", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Lỗi khi reset: {ex.Message}", 
                    "Lỗi", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Error
                );
            }
        }

        // ====== SỰ KIỆN ĐÓNG FORM ======

        /// <summary>
        /// Sự kiện khi người dùng đóng form
        /// 
        /// Tác dụng: Giải phóng tất cả tài nguyên
        /// 
        /// Quan trọng: 
        /// - OpenCV Mat objects sử dụng unmanaged memory (C++)
        /// - Nếu không giải phóng → Memory leak
        /// - GC (Garbage Collector) không tự động xóa unmanaged memory
        /// - Phải gọi Dispose() hoặc using statement
        /// </summary>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                // === GIẢI PHÓNG MAT OBJECTS ===
                originalImage?.Dispose();
                preprocessedImage?.Dispose();
                finalImage?.Dispose();

                // === GIẢI PHÓNG BITMAP OBJECTS ===
                pictureBox1.Image?.Dispose();
                pictureBox2.Image?.Dispose();
                pictureBox3.Image?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi đóng form: {ex.Message}");
            }
        }
    }
}
