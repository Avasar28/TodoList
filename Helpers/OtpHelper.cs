namespace TodoListApp.Helpers
{
    public static class OtpHelper
    {
        public static string Generate6DigitOtp()
        {
            return new Random().Next(100000, 999999).ToString();
        }

        public static string GetOtpEmailBody(string otp)
        {
            return $@"
                <div style='font-family: sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 10px; max-width: 500px;'>
                    <h2 style='color: #4F46E5;'>Email Verification</h2>
                    <p>Thank you for signing up! Please use the following code to verify your email address:</p>
                    <div style='font-size: 32px; font-weight: bold; letter-spacing: 5px; color: #111827; padding: 20px 0;'>
                        {otp}
                    </div>
                    <p style='color: #6B7280; font-size: 14px;'>This code will expire in 10 minutes.</p>
                    <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='font-size: 12px; color: #9CA3AF;'>If you didn't request this, please ignore this email.</p>
                </div>";
        }

        public static string GetResetPasswordEmailBody(string resetLink)
        {
            return $@"
                <div style='font-family: sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 10px; max-width: 500px;'>
                    <h2 style='color: #4F46E5;'>Reset Your Password</h2>
                    <p>We received a request to reset your password. Click the button below to proceed:</p>
                    <div style='padding: 20px 0;'>
                        <a href='{resetLink}' style='background-color: #4F46E5; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold;'>Reset Password</a>
                    </div>
                    <p style='color: #6B7280; font-size: 14px;'>This link will expire in 1 hour.</p>
                    <p style='color: #9CA3AF; font-size: 12px;'>If the button doesn't work, copy and paste this link: <br>{resetLink}</p>
                    <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='font-size: 12px; color: #9CA3AF;'>If you didn't request this, please ignore this email.</p>
                </div>";
        }
    }
}
