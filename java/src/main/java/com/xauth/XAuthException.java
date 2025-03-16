package com.xauth;

/**
 * Twitter OAuth认证异常
 */
public class XAuthException extends Exception {
    
    /**
     * 创建一个新的XAuthException实例
     * 
     * @param message 异常消息
     */
    public XAuthException(String message) {
        super(message);
    }
    
    /**
     * 创建一个新的XAuthException实例
     * 
     * @param message 异常消息
     * @param cause 原始异常
     */
    public XAuthException(String message, Throwable cause) {
        super(message, cause);
    }
} 