using BeautyShop.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace BeautyShop.Services
{
    public interface IEmailService
    {
        Task SendOrderConfirmationEmailAsync(Order order, string recipientEmail);
        Task SendOrderStatusUpdateEmailAsync(Order order, string recipientEmail, string oldStatus, string newStatus);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private async Task SendEmailInternalAsync(string toEmail, string subject, string bodyHtml)
        {
            try
            {
                var smtpServer = _config["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_config["EmailSettings:SmtpPort"] ?? "587");
                var senderEmail = _config["EmailSettings:SenderEmail"] ?? "vutran03092005@gmail.com";
                var senderName = _config["EmailSettings:SenderName"] ?? "BeautyShop - Mỹ Phẩm Chính Hãng";
                var username = _config["EmailSettings:Username"] ?? "vutran03092005@gmail.com";
                var password = (_config["EmailSettings:Password"] ?? "").Replace(" ", "");
                var enableSsl = bool.Parse(_config["EmailSettings:EnableSsl"] ?? "true");

                if (string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogWarning($"[EmailService Demo] Mật khẩu ứng dụng trống.");
                    return;
                }

                // Ép phân giải IPv4 để tránh lỗi SocketException trên Windows không bật IPv6
                string connectHost = smtpServer;
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(smtpServer);
                    var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (ipv4 != null)
                    {
                        connectHost = ipv4.ToString();
                    }
                }
                catch
                {
                }

                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(senderEmail, senderName, Encoding.UTF8);
                    message.To.Add(new MailAddress(toEmail));
                    message.Subject = subject;
                    message.Body = bodyHtml;
                    message.IsBodyHtml = true;
                    message.BodyEncoding = Encoding.UTF8;
                    message.SubjectEncoding = Encoding.UTF8;

                    using (var client = new SmtpClient(connectHost, smtpPort))
                    {
                        client.Credentials = new NetworkCredential(username, password);
                        client.EnableSsl = enableSsl;
                        await client.SendMailAsync(message);
                    }
                }

                _logger.LogInformation($"[EmailService SUCCESS] Đã gửi Email thành công tới: {toEmail} | Subject: {subject}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[EmailService ERROR] Lỗi khi gửi mail tới {toEmail}: {ex.Message}");
            }
        }

        public Task SendOrderConfirmationEmailAsync(Order order, string recipientEmail)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                recipientEmail = _config["EmailSettings:SenderEmail"] ?? "vutran03092005@gmail.com";
            }

            _ = Task.Run(async () =>
            {
                string subject = $"[BeautyShop] Xác nhận đơn hàng #{order.OrderCode} thành công";

                var itemsHtml = new StringBuilder();
                if (order.OrderDetails != null)
                {
                    foreach (var item in order.OrderDetails)
                    {
                        string prodName = item.Variant?.Product?.Name ?? "Sản phẩm mỹ phẩm";
                        itemsHtml.Append($@"
                            <tr>
                                <td style='padding:12px;border-bottom:1px solid #edf2f7;font-weight:600;color:#2d3748;'>{prodName}</td>
                                <td style='padding:12px;border-bottom:1px solid #edf2f7;text-align:center;'>{item.Quantity}</td>
                                <td style='padding:12px;border-bottom:1px solid #edf2f7;text-align:right;'>{item.UnitPrice:N0}đ</td>
                                <td style='padding:12px;border-bottom:1px solid #edf2f7;text-align:right;font-weight:700;color:#006E4A;'>{(item.Quantity * item.UnitPrice):N0}đ</td>
                            </tr>
                        ");
                    }
                }

                string body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 650px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; background: #ffffff;'>
                        <div style='background: linear-gradient(135deg, #006E4A, #009665); padding: 25px; text-align: center; color: #ffffff;'>
                            <h1 style='margin: 0; font-size: 24px; font-weight: 800;'>BeautyShop</h1>
                            <p style='margin: 5px 0 0; opacity: 0.9; font-size: 14px;'>Cảm ơn bạn đã đặt hàng tại BeautyShop!</p>
                        </div>
                        
                        <div style='padding: 25px;'>
                            <h2 style='color: #006E4A; font-size: 18px; margin-top: 0;'>Xác nhận đơn hàng #{order.OrderCode}</h2>
                            <p style='color: #4a5568; font-size: 14px; line-height: 1.6;'>
                                Xin chào <strong>{order.CustomerName}</strong>,<br>
                                Đơn hàng của bạn đã được hệ thống BeautyShop ghi nhận thành công và đang chờ xác nhận từ cửa hàng.
                            </p>

                            <div style='background: #f7fafc; border-radius: 8px; padding: 15px; margin: 20px 0; border: 1px solid #edf2f7;'>
                                <h3 style='margin: 0 0 10px; font-size: 15px; color: #2d3748;'>Thông tin người nhận</h3>
                                <p style='margin: 3px 0; color: #718096; font-size: 13px;'><strong>Người nhận:</strong> {order.CustomerName}</p>
                                <p style='margin: 3px 0; color: #718096; font-size: 13px;'><strong>Số điện thoại:</strong> {order.PhoneNumber}</p>
                                <p style='margin: 3px 0; color: #718096; font-size: 13px;'><strong>Địa chỉ giao hàng:</strong> {order.ShippingAddress}</p>
                                <p style='margin: 3px 0; color: #718096; font-size: 13px;'><strong>Phương thức thanh toán:</strong> {order.PaymentMethod}</p>
                            </div>

                            <h3 style='color: #2d3748; font-size: 16px; margin-bottom: 10px;'>Chi tiết đơn hàng</h3>
                            <table style='width: 100%; border-collapse: collapse; font-size: 13px;'>
                                <thead>
                                    <tr style='background: #e6f5ef; color: #006E4A;'>
                                        <th style='padding: 10px; text-align: left;'>Sản phẩm</th>
                                        <th style='padding: 10px; text-align: center;'>SL</th>
                                        <th style='padding: 10px; text-align: right;'>Đơn giá</th>
                                        <th style='padding: 10px; text-align: right;'>Thành tiền</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {itemsHtml}
                                </tbody>
                            </table>

                            <div style='text-align: right; margin-top: 20px; font-size: 14px; color: #4a5568;'>
                                <p style='margin: 5px 0;'>Tạm tính: <strong>{order.TotalAmount:N0}đ</strong></p>
                                <p style='margin: 5px 0;'>Phí vận chuyển: <strong>{order.DeliveryFee:N0}đ</strong></p>
                                {(order.DiscountAmount > 0 ? $"<p style='margin: 5px 0; color: #e53e3e;'>Giảm giá: <strong>-{order.DiscountAmount:N0}đ</strong></p>" : "")}
                                <h3 style='margin: 10px 0 0; color: #006E4A; font-size: 20px;'>Tổng thanh toán: {order.FinalAmount:N0}đ</h3>
                            </div>
                        </div>

                        <div style='background: #f7fafc; padding: 15px; text-align: center; font-size: 12px; color: #a0aec0; border-top: 1px solid #edf2f7;'>
                            BeautyShop - Cửa Hàng Mỹ Phẩm Chính Hãng<br>
                            Mọi thắc mắc xin liên hệ hotline hoặc phản hồi email này.
                        </div>
                    </div>
                ";

                await SendEmailInternalAsync(recipientEmail, subject, body);
            });

            return Task.CompletedTask;
        }

        public Task SendOrderStatusUpdateEmailAsync(Order order, string recipientEmail, string oldStatus, string newStatus)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                recipientEmail = _config["EmailSettings:SenderEmail"] ?? "vutran03092005@gmail.com";
            }

            _ = Task.Run(async () =>
            {
                string statusVietnamese = newStatus switch
                {
                    "Confirmed" or "Đã xác nhận" => "Đã được xác nhận & Đang chuẩn bị hàng",
                    "Shipping" or "Đang giao hàng" => "Đang trên đường giao đến bạn",
                    "Completed" or "Giao thành công" => "Đã giao thành công",
                    "Cancelled" or "Hủy bỏ đơn" or "Đã hủy" => "Đã được hủy",
                    _ => newStatus
                };

                string subject = $"[BeautyShop] Đơn hàng #{order.OrderCode} - Cập nhật trạng thái: {statusVietnamese}";

                string body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 650px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; background: #ffffff;'>
                        <div style='background: linear-gradient(135deg, #006E4A, #009665); padding: 25px; text-align: center; color: #ffffff;'>
                            <h1 style='margin: 0; font-size: 24px; font-weight: 800;'>BeautyShop</h1>
                            <p style='margin: 5px 0 0; opacity: 0.9; font-size: 14px;'>Thông báo cập nhật trạng thái đơn hàng</p>
                        </div>
                        
                        <div style='padding: 25px;'>
                            <h2 style='color: #006E4A; font-size: 18px; margin-top: 0;'>Đơn hàng #{order.OrderCode}</h2>
                            <p style='color: #4a5568; font-size: 14px; line-height: 1.6;'>
                                Xin chào <strong>{order.CustomerName}</strong>,<br>
                                Đơn hàng <strong>#{order.OrderCode}</strong> của bạn vừa có cập nhật trạng thái mới.
                            </p>

                            <div style='background: #e6f5ef; border-left: 4px solid #006E4A; padding: 15px; margin: 20px 0; border-radius: 4px;'>
                                <p style='margin: 0; font-size: 15px; color: #006E4A;'>
                                    Trạng thái mới: <strong>{statusVietnamese}</strong>
                                </p>
                                {(string.IsNullOrEmpty(order.GhnOrderCode) ? "" : $"<p style='margin: 8px 0 0; font-size: 13px; color: #2d3748;'>Mã vận đơn GHN: <strong>{order.GhnOrderCode}</strong></p>")}
                            </div>

                            <p style='color: #718096; font-size: 13px;'>
                                Địa chỉ nhận hàng: <strong>{order.ShippingAddress}</strong><br>
                                Tổng thanh toán: <strong style='color:#006E4A;'>{order.FinalAmount:N0}đ</strong>
                            </p>
                        </div>

                        <div style='background: #f7fafc; padding: 15px; text-align: center; font-size: 12px; color: #a0aec0; border-top: 1px solid #edf2f7;'>
                            BeautyShop - Cửa Hàng Mỹ Phẩm Chính Hãng
                        </div>
                    </div>
                ";

                await SendEmailInternalAsync(recipientEmail, subject, body);
            });

            return Task.CompletedTask;
        }
    }
}
